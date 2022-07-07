// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using Microsoft.Extensions.Options;
using Squidex.Hosting;

namespace Squidex.Messaging.Implementation
{
    public sealed class DelegatingProducer : IInternalMessageProducer, IInitializable
    {
        private readonly string activity;
        private readonly string channelName;
        private readonly ITransport transport;
        private readonly ITransportSerializer serializer;
        private readonly IClock clock;
        private readonly ChannelOptions channelOptions;

        public string ChannelName => channelName;

        public DelegatingProducer(
            string channelName,
            ITransportFactory transportProvider,
            ITransportSerializer serializer,
            IOptionsMonitor<ChannelOptions> channelOptions,
            IClock clock)
        {
            activity = $"Messaging.Produce({channelName})";

            this.serializer = serializer;
            this.clock = clock;
            this.channelName = channelName;
            this.channelOptions = channelOptions.Get(channelName);
            this.transport = transportProvider.GetTransport(channelName);
        }

        public DelegatingProducer()
        {
        }

        public Task InitializeAsync(
            CancellationToken ct)
        {
            return transport.InitializeAsync(channelOptions, ct);
        }

        public Task ReleaseAsync(
            CancellationToken ct)
        {
            return transport.ReleaseAsync(ct);
        }

        public async Task ProduceAsync(object message, string? key = null,
            CancellationToken ct = default)
        {
            using (MessagingTelemetry.Activities.StartActivity(activity))
            {
                var data = serializer.Serialize(message);

                if (string.IsNullOrEmpty(key))
                {
                    key = Guid.NewGuid().ToString();
                }

                var headers = new TransportHeaders()
                    .Set(Headers.Id, Guid.NewGuid())
                    .Set(Headers.Type, message?.GetType().AssemblyQualifiedName ?? "null")
                    .Set(Headers.TimeExpires, channelOptions.Expires)
                    .Set(Headers.TimeTimeout, channelOptions.Timeout)
                    .Set(Headers.TimeCreated, clock.UtcNow);

                var transportMessage = new TransportMessage(data, key, headers);

                await transport.ProduceAsync(transportMessage, ct);
            }
        }
    }
}
