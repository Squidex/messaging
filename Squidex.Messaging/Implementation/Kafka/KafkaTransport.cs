﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;
using Squidex.Messaging.Internal;

namespace Squidex.Messaging.Implementation.Kafka
{
    public sealed class KafkaTransport : ITransport
    {
        private readonly string channelName;
        private readonly KafkaTransportFactory factory;
        private readonly ILogger<KafkaTransport> log;
        private IProducer<string, byte[]>? producer;

        public KafkaTransport(string channelName, KafkaTransportFactory factory,
            ILogger<KafkaTransport> log)
        {
            this.channelName = channelName;
            this.factory = factory;
            this.log = log;
        }

        public Task InitializeAsync(ChannelOptions channelOptions,
            CancellationToken ct)
        {
            producer =
                new DependentProducerBuilder<string, byte[]>(factory.Handle)
                    .Build();

            return Task.CompletedTask;
        }

        public Task ReleaseAsync(
            CancellationToken ct)
        {
            if (producer != null)
            {
                producer.Flush(ct);
                producer.Dispose();
            }

            return Task.CompletedTask;
        }

        public void CleanupOldEntries(TimeSpan timeout, TimeSpan expires)
        {
        }

        public async Task ProduceAsync(TransportMessage transportMessage,
            CancellationToken ct = default)
        {
            if (producer == null)
            {
                ThrowHelper.InvalidOperationException("Transport not initialized yet.");
                return;
            }

            var messageKey = transportMessage.Key;

            if (string.IsNullOrWhiteSpace(messageKey))
            {
                messageKey = Guid.NewGuid().ToString();
            }

            var message = new Message<string, byte[]> { Value = transportMessage.Data, Key = messageKey };

            foreach (var (key, value) in transportMessage.Headers)
            {
                message.Headers.Add(key, Encoding.UTF8.GetBytes(value));
            }

            try
            {
                producer.Produce(channelName, message);
            }
            catch (ProduceException<string, byte[]> ex) when (ex.Error.Code == ErrorCode.Local_QueueFull)
            {
                while (true)
                {
                    try
                    {
                        producer.Poll(Timeout.InfiniteTimeSpan);

                        await producer.ProduceAsync(channelName, message, ct);

                        return;
                    }
                    catch (ProduceException<string, byte[]> ex2) when (ex2.Error.Code == ErrorCode.Local_QueueFull)
                    {
                        await Task.Delay(100, ct);
                    }
                }
            }
        }

        public Task<IAsyncDisposable> SubscribeAsync(MessageTransportCallback callback,
            CancellationToken ct = default)
        {
            var subscription = new KafkaSubscription(factory, channelName, callback, log);

            return Task.FromResult<IAsyncDisposable>(subscription);
        }
    }
}
