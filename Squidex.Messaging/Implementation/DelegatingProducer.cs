// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using Microsoft.Extensions.Options;
using Squidex.Hosting;
using Squidex.Messaging.Internal;

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

        public string Name => $"Messaging.Producer({channelName})";

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
            Guard.NotNull(message, nameof(message));

            using (MessagingTelemetry.Activities.StartActivity(activity))
            {
                var data = serializer.Serialize(message);

                if (string.IsNullOrEmpty(key))
                {
                    key = Guid.NewGuid().ToString();
                }

                var typeName = message?.GetType().AssemblyQualifiedName;

                if (string.IsNullOrWhiteSpace(typeName))
                {
                    ThrowHelper.ArgumentException("Cannot calculate type name.", nameof(message));
                    return;
                }

                var headers = new TransportHeaders()
                    .Set(HeaderNames.Id, Guid.NewGuid())
                    .Set(HeaderNames.Type, typeName)
                    .Set(HeaderNames.TimeExpires, channelOptions.Expires)
                    .Set(HeaderNames.TimeTimeout, channelOptions.Timeout)
                    .Set(HeaderNames.TimeCreated, clock.UtcNow);

                var transportMessage = new TransportMessage(data, key, headers);

                await transport.ProduceAsync(transportMessage, ct);
            }
        }
    }
}
