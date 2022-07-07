// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using System.Text;
using Confluent.Kafka;
using Microsoft.Extensions.Logging;

namespace Squidex.Messaging.Implementation.Kafka
{
    public sealed class KafkaSubscription : IAsyncDisposable, IMessageAck
    {
        private readonly ILogger log;
        private readonly CancellationTokenSource cancellationTokenSource = new CancellationTokenSource();
        private readonly Thread consumerThread;
        private readonly IConsumer<string, byte[]> consumer;

        public KafkaSubscription(KafkaTransportFactory factory, string channelName, MessageTransportCallback callback,
            ILogger log)
        {
            this.log = log;

            var config = factory.Options.Configure(new ConsumerConfig
            {
                AutoOffsetReset = AutoOffsetReset.Earliest,
            });

            // We do not commit automatically, because the message might be scheduled.
            config.EnableAutoCommit = false;

            consumer =
                new ConsumerBuilder<string, byte[]>(config)
                    .SetLogHandler(KafkaLogFactory<string, byte[]>.ConsumerLog(log))
                    .SetErrorHandler(KafkaLogFactory<string, byte[]>.ConsumerError(log))
                    .SetStatisticsHandler(KafkaLogFactory<string, byte[]>.ConsumerStats(log))
                    .Build();

            var consume = new ThreadStart(() =>
            {
                using (consumer)
                {
                    consumer.Subscribe(channelName);

                    try
                    {
                        while (!cancellationTokenSource.IsCancellationRequested)
                        {
                            var result = consumer.Consume(cancellationTokenSource.Token);

                            var headers = new TransportHeaders();

                            foreach (var header in result.Message.Headers)
                            {
                                headers.Set(header.Key, Encoding.UTF8.GetString(header.GetValueBytes()));
                            }

                            var transportMessage = new TransportMessage(result.Message.Value, result.Message.Key, headers);
                            var transportResult = new TransportResult(transportMessage, result);

                            callback(transportResult, this, cancellationTokenSource.Token).Wait(cancellationTokenSource.Token);
                        }
                    }
                    finally
                    {
                        consumer.Close();
                    }
                }
            });

#pragma warning disable IDE0017 // Simplify object initialization
            consumerThread = new Thread(consume);
            consumerThread.Name = "MessagingConsumer";
            consumerThread.IsBackground = true;
            consumerThread.Start();
#pragma warning restore IDE0017 // Simplify object initialization
        }

        public ValueTask DisposeAsync()
        {
            try
            {
                cancellationTokenSource.Cancel();
                consumerThread.Join(1500);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Kafka shutdown failed.");
            }

            return default;
        }

        public Task OnErrorAsync(TransportResult result,
            CancellationToken ct = default)
        {
            Commit(result);
            return Task.CompletedTask;
        }

        public Task OnSuccessAsync(TransportResult result,
            CancellationToken ct = default)
        {
            Commit(result);
            return Task.CompletedTask;
        }

        private void Commit(TransportResult result)
        {
            if (cancellationTokenSource.IsCancellationRequested || result.Data is not ConsumeResult<string, byte[]> consumeResult)
            {
                log.LogWarning("Transport message has no consume result.");
                return;
            }

            try
            {
                consumer.Commit(consumeResult);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to commit the message.");
            }
        }
    }
}
