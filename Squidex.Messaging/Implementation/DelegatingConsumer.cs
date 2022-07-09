// ==========================================================================
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
using Squidex.Messaging.Implementation.Scheduler;

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
        private readonly HandlerPipeline pipeline;
        private readonly IScheduler scheduler;
        private readonly ITransportSerializer serializer;
        private readonly ITransport transport;
        private readonly ILogger<DelegatingConsumer> log;
        private bool isReleased;

        public string Name => $"Messaging.Consumer({channelName})";

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
            this.scheduler = this.channelOptions.Scheduler ?? InlineScheduler.Instance;
            this.serializer = serializer;
            this.log = log;
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

        private async Task OnScheduledMessage((Type type, TransportResult TtransportResult, IMessageAck Ack) args,
            CancellationToken ct)
        {
            var (type, transportResult, ack) = args;
            try
            {
                object? message = null;

                try
                {
                    message = serializer.Deserialize(transportResult.Message.Data, type);
                }
                catch (Exception ex)
                {
                    log.LogError(ex, "Failed to deserialize message with type {type}.", type);
                    return;
                }

                if (message == null)
                {
                    log.LogError("Failed to deserialize message with type {type}.", type);
                    return;
                }

                var handlers = pipeline.GetHandlers(type);

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
                            using (var linked = CancellationTokenSource.CreateLinkedTokenSource(cts.Token, ct))
                            {
                                await handler(message, linked.Token);
                            }
                        }
                    }
                    catch (OperationCanceledException)
                    {
                        continue;
                    }
                    catch (Exception ex)
                    {
                        log.LogError(ex, "Failed to consume message for system {system} with type {type}.", Name, type);
                    }
                }
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to consume message with type {type}.", type);
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

                var type = Type.GetType(typeString);

                if (type == null)
                {
                    // The message is broken, we cannot handle it, even if we would retry.
                    await ack.OnSuccessAsync(transportResult, default);

                    log.LogWarning("Message has invalid or unknown type {type}.", typeString);
                    return;
                }

                await scheduler.ExecuteAsync((type, transportResult, ack), (args, ct) => OnScheduledMessage(args, ct), ct);
            }
        }
    }
}
