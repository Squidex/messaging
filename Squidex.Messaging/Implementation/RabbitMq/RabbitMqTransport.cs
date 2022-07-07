// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using Microsoft.Extensions.Logging;
using RabbitMQ.Client;
using Squidex.Messaging.Internal;

namespace Squidex.Messaging.Implementation.RabbitMq
{
    public sealed class RabbitMqTransport : ITransport
    {
        private readonly RabbitMqTransportFactory factory;
        private readonly ILogger<RabbitMqTransport> log;
        private readonly string channelName;
        private IModel? model;

        public RabbitMqTransport(string channelName,
            RabbitMqTransportFactory factory,
            ILogger<RabbitMqTransport> log)
        {
            this.channelName = channelName;
            this.factory = factory;
            this.log = log;
        }

        public Task InitializeAsync(ChannelOptions options,
            CancellationToken ct)
        {
            if (model != null)
            {
                return Task.CompletedTask;
            }

            model = factory.Connection.CreateModel();

            lock (model)
            {
                model.QueueDeclare(channelName, true, false, false, null);
            }

            return Task.CompletedTask;
        }

        public Task ReleaseAsync(
            CancellationToken ct)
        {
            if (model == null)
            {
                return Task.CompletedTask;
            }

            model.Dispose();
            model = null;

            return Task.CompletedTask;
        }

        public void CleanupOldEntries(TimeSpan timeout, TimeSpan expires)
        {
        }

        public Task ProduceAsync(TransportMessage transportMessage,
            CancellationToken ct)
        {
            if (model == null)
            {
                ThrowHelper.InvalidOperationException("Transport not initialized yet.");
                return Task.CompletedTask;
            }

            lock (model)
            {
                var properties = model.CreateBasicProperties();

                if (transportMessage.Headers.Count > 0)
                {
                    properties.Headers = new Dictionary<string, object>();

                    foreach (var (key, value) in transportMessage.Headers)
                    {
                        properties.Headers[key] = value;
                    }
                }

                model.BasicPublish(string.Empty, channelName, properties, transportMessage.Data);
            }

            return Task.CompletedTask;
        }

        public Task<IAsyncDisposable> SubscribeAsync(MessageTransportCallback callback,
            CancellationToken ct)
        {
            var subscription = new RabbitMqSubscription(channelName, factory, callback, log);

            return Task.FromResult<IAsyncDisposable>(subscription);
        }
    }
}
