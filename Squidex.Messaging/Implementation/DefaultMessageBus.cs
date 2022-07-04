// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using Microsoft.Extensions.Options;

namespace Squidex.Messaging.Implementation
{
    internal sealed class DefaultMessageBus : IMessageBus
    {
        private readonly Dictionary<string, IInternalMessageProducer> producers;
        private readonly Dictionary<Func<object, bool>, string>? routing;

        public DefaultMessageBus(IEnumerable<IInternalMessageProducer> producers,
            IOptions<MessagingOptions> options)
        {
            this.producers = producers.ToDictionary(x => x.ChannelName);

            routing = options.Value.Routing?.ToDictionary(x => x.Predicate, x => x.ChannelName);
        }

        public async Task PublishAsync(object message, string? key = null,
            CancellationToken ct = default)
        {
            if (routing != null)
            {
                foreach (var (predicate, channelName) in routing)
                {
                    if (predicate(message))
                    {
                        await PublishToChannelAsync(message, channelName, key, ct);
                        return;
                    }
                }
            }

            throw new InvalidOperationException("Cannot find a matching channel name.");
        }

        public Task PublishToChannelAsync(object message, string channelName, string? key = null,
            CancellationToken ct = default)
        {
            if (!producers.TryGetValue(channelName, out var producer))
            {
                throw new InvalidOperationException($"Cannot find channel '{channelName}'");
            }

            return producer.ProduceAsync(message, key, ct);
        }
    }
}
