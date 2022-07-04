﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using Microsoft.Extensions.Options;
using Squidex.Hosting;

namespace Squidex.Messaging.Implementation
{
    public sealed class DelegatingProducer<T> : IMessageProducer<T>, IInitializable
    {
        private readonly string activity = $"Messaging.Produce({typeof(T).Name})";
        private readonly ITransport transport;
        private readonly ITransportSerializer serializer;
        private readonly IClock clock;
        private readonly MessagingOptions<T> options;

        public DelegatingProducer(
            ITransportFactory transportProvider,
            ITransportSerializer serializer,
            IOptions<MessagingOptions<T>> options, IClock clock)
        {
            this.serializer = serializer;
            this.clock = clock;
            this.options = options.Value;
            this.transport = transportProvider.GetTransport(options.Value.ChannelName);
        }

        public Task InitializeAsync(
            CancellationToken ct)
        {
            return transport.InitializeAsync(ct);
        }

        public Task ReleaseAsync(
            CancellationToken ct)
        {
            return transport.ReleaseAsync(ct);
        }

        public async Task ProduceAsync(T message, string? key = null,
            CancellationToken ct = default)
        {
            using (MessagingTelemetry.Activities.StartActivity(activity))
            {
                var data = serializer.Serialize(message);

                if (string.IsNullOrEmpty(key))
                {
                    key = Guid.NewGuid().ToString();
                }

                var transportMessage = new TransportMessage(data)
                {
                    Key = key,
                    Headers = new Dictionary<string, string>
                    {
                        [Headers.Id] = Guid.NewGuid().ToString(),
                        [Headers.Type] = message?.GetType().AssemblyQualifiedName ?? "null",
                        [Headers.TimeExpires] = options.Expires.ToString(),
                        [Headers.TimeRetry] = options.Timeout.ToString(),
                        [Headers.Key] = key ?? string.Empty
                    },
                    Created = clock.UtcNow
                };

                await transport.ProduceAsync(transportMessage, ct);
            }
        }
    }
}
