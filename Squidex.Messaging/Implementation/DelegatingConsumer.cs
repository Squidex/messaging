﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using System.Diagnostics;
using System.Threading.Tasks.Dataflow;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Squidex.Hosting;

#pragma warning disable SA1313 // Parameter names should begin with lower-case letter
#pragma warning disable RECS0082 // Parameter has the same name as a member and hides it

namespace Squidex.Messaging.Implementation
{
    internal sealed class DelegatingConsumer : IInitializable, IBackgroundProcess
    {
        private readonly string activity;
        private readonly string channelName;
        private readonly List<IAsyncDisposable> subscriptions = new List<IAsyncDisposable>();
        private readonly ChannelOptions channelOptions;
        private readonly ActionBlock<ScheduledMessage> worker;
        private readonly HandlerPipeline pipeline;
        private readonly ITransportSerializer serializer;
        private readonly ITransport transport;
        private readonly ILogger<DelegatingConsumer> log;
        private bool isReleased;

        sealed record ScheduledMessage(Type Type, TransportResult Result, IMessageAck Ack);

        public string Name => activity;

        public string ChannelName => channelName;

        public int Order => int.MaxValue;

        public DelegatingConsumer(
            string channelName,
            HandlerPipeline pipeline,
            ITransportSerializer serializer,
            ITransportFactory transportFactory,
            IOptionsMonitor<ChannelOptions> channelOptions,
            ILogger<DelegatingConsumer> log)
        {
            activity = $"Messaging.Consume({channelName})";

            transport = transportFactory.GetTransport(channelName);

            this.pipeline = pipeline;
            this.channelName = channelName;
            this.channelOptions = channelOptions.Get(channelName);
            this.serializer = serializer;
            this.log = log;

            worker = new ActionBlock<ScheduledMessage>(OnScheduledMessage, new ExecutionDataflowBlockOptions
            {
                MaxMessagesPerTask = 1,
                MaxDegreeOfParallelism = this.channelOptions.NumWorkers,
                BoundedCapacity = 1
            });
        }

        public async Task InitializeAsync(
            CancellationToken ct)
        {
            await transport.InitializeAsync(channelOptions, ct);
        }

        public async Task StartAsync(
            CancellationToken ct)
        {
            if (pipeline.HasHandlers)
            {
                for (var i = 0; i < channelOptions.NumSubscriptions; i++)
                {
                    var subscription = await transport.SubscribeAsync(OnMessageAsync, ct);

                    subscriptions.Add(subscription);
                }

                transport.CleanupOldEntries(channelOptions.Timeout, channelOptions.Expires);
            }
        }

        public async Task ReleaseAsync(
            CancellationToken ct)
        {
            isReleased = true;

            foreach (var subscription in subscriptions)
            {
                await subscription.DisposeAsync();
            }

            await transport.ReleaseAsync(ct);
        }

        private async Task OnScheduledMessage(ScheduledMessage input)
        {
            var (type, transportResult, ack) = input;

            object message;
            try
            {
                message = serializer.Deserialize(transportResult.Message.Data, type);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to deserialize message with type {type}.", type);
                return;
            }

            try
            {
                var handlers = pipeline.GetHandlers(message);

                foreach (var handler in handlers)
                {
                    if (isReleased)
                    {
                        return;
                    }

                    try
                    {
                        using (var cts = new CancellationTokenSource(channelOptions.Timeout))
                        {
                            await handler(message, cts.Token);
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        continue;
                    }
                    catch (Exception ex)
                    {
                        log.LogError(ex, "Failed to consume message for system {system}.", Name);
                    }
                }
            }
            finally
            {
                if (!isReleased)
                {
                    // Ignore cancellation, better to delete the message, even if cancelled.
                    await ack.OnSuccessAsync(transportResult, default);
                }
            }
        }

        private async Task OnMessageAsync(TransportResult transportResult, IMessageAck ack,
            CancellationToken ct)
        {
            if (isReleased)
            {
                return;
            }

            using (var trace = MessagingTelemetry.Activities.StartActivity(activity))
            {
                transportResult.Message.Headers.TryGetDateTime(HeaderNames.TimeCreated, out var created);

                if (created != default && trace?.Id != null)
                {
                    MessagingTelemetry.Activities.StartActivity("QueueTime", ActivityKind.Internal, trace.Id,
                        startTime: created)?.Stop();
                }

                var typeString = transportResult.Message.Headers?.GetValueOrDefault(HeaderNames.Type) ?? string.Empty;

                if (string.IsNullOrWhiteSpace(typeString))
                {
                    // The message is broken, we cannot handle it, even if we would retry.
                    await ack.OnSuccessAsync(transportResult, default);

                    log.LogWarning("Message has no type header.");
                    return;
                }

                if (typeString == "null")
                {
                    await worker.SendAsync(new ScheduledMessage(default!, transportResult, ack), ct);
                    return;
                }

                var type = Type.GetType(typeString);

                if (type == null)
                {
                    // The message is broken, we cannot handle it, even if we would retry.
                    await ack.OnSuccessAsync(transportResult, default);

                    log.LogWarning("Message has invalid or unknown type {type}.", typeString);
                    return;
                }

                await worker.SendAsync(new ScheduledMessage(type, transportResult, ack), ct);
            }
        }
    }
}
