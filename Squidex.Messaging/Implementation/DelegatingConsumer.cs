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

#pragma warning disable CA2016 // Forward the 'CancellationToken' parameter to methods
#pragma warning disable MA0040 // Flow the cancellation token
#pragma warning disable SA1313 // Parameter names should begin with lower-case letter
#pragma warning disable RECS0082 // Parameter has the same name as a member and hides it

namespace Squidex.Messaging.Implementation
{
    public sealed class DelegatingConsumer<T> : IInitializable
    {
        private readonly List<IAsyncDisposable> subscriptions = new List<IAsyncDisposable>();
        private readonly MessagingOptions<T> options;
        private readonly ActionBlock<ScheduledMessage> worker;
        private readonly ITransportSerializer serializer;
        private readonly ITransport transport;
        private readonly IEnumerable<IMessageHandler<T>> handlers;
        private readonly ILogger<DelegatingConsumer<T>> log;
        private readonly string activity = $"Messaging.Consume({typeof(T).Name})";
        private bool isReleased;

        sealed record ScheduledMessage(T Message, TransportMessage TransportMessage, IMessageAck Ack);

        public string Name => $"MessagingConsumer({typeof(T).Name})";

        public int Order => int.MaxValue;

        public DelegatingConsumer(
            IEnumerable<IMessageHandler<T>> handlers,
            ITransportSerializer serializer,
            ITransportFactory transportFactory,
            IOptions<MessagingOptions<T>> options,
            ILogger<DelegatingConsumer<T>> log)
        {
            transport = transportFactory.GetTransport(options.Value.ChannelName);

            this.handlers = handlers;
            this.options = options.Value;
            this.serializer = serializer;
            this.log = log;

            worker = new ActionBlock<ScheduledMessage>(OnSerializedMessage, new ExecutionDataflowBlockOptions
            {
                MaxMessagesPerTask = 1,
                MaxDegreeOfParallelism = options.Value.NumWorkers,
                BoundedCapacity = 1
            });
        }

        public async Task InitializeAsync(
            CancellationToken ct)
        {
            await transport.InitializeAsync(ct);

            if (handlers.Any())
            {
                for (var i = 0; i < options.NumSubscriptions; i++)
                {
                    var subscription = await transport.SubscribeAsync(OnMessageAsync, ct);

                    subscriptions.Add(subscription);
                }

                transport.CleanupOldEntries(options.Timeout, options.Expires);
            }
        }

        public async Task ReleaseAsync(
            CancellationToken ct)
        {
            isReleased = true;

            await transport.ReleaseAsync(ct);
        }

        private async Task OnSerializedMessage(ScheduledMessage input)
        {
            var (message, transportMessage, ack) = input;

            try
            {
                foreach (var handler in handlers)
                {
                    if (isReleased)
                    {
                        return;
                    }

                    try
                    {
                        using (var cts = new CancellationTokenSource(options.Timeout))
                        {
                            await handler.HandleAsync(message, cts.Token);
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
                    await ack.OnSuccessAsync(transportMessage);
                }
            }
        }

        private async Task OnMessageAsync(TransportMessage transportMessage, IMessageAck ack,
            CancellationToken ct)
        {
            if (isReleased)
            {
                return;
            }

            using (var trace = MessagingTelemetry.Activities.StartActivity(activity))
            {
                if (transportMessage.Created != default && trace?.Id != null)
                {
                    MessagingTelemetry.Activities.StartActivity("QueueTime", ActivityKind.Internal, trace.Id,
                        startTime: transportMessage.Created)?.Stop();
                }

                var typeString = transportMessage.Headers?.GetValueOrDefault(Headers.Type) ?? string.Empty;

                if (string.IsNullOrWhiteSpace(typeString))
                {
                    await ack.OnSuccessAsync(transportMessage);

                    log.LogWarning("Message has no type header.");
                    return;
                }

                if (typeString == "null")
                {
                    await worker.SendAsync(new ScheduledMessage(default!, transportMessage, ack), ct);
                    return;
                }

                var type = Type.GetType(typeString);

                if (type == null)
                {
                    await ack.OnSuccessAsync(transportMessage);

                    log.LogWarning("Message has invalid or unknown type {type}.", typeString);
                    return;
                }

                var message = serializer.Deserialize<T>(transportMessage.Data, type);

                await worker.SendAsync(new ScheduledMessage(message, transportMessage, ack), ct);
            }
        }
    }
}
