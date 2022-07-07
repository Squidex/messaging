// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using Confluent.Kafka;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Squidex.Messaging.Implementation.Kafka
{
    public sealed class KafkaTransportFactory : ITransportFactory
    {
        private readonly Func<string, ITransport> factory;
        private readonly IProducer<Null, Null> producer;

        public Handle Handle => producer.Handle;

        public KafkaTransportOptions Options { get; }

        public KafkaTransportFactory(IServiceProvider serviceProvider,
            IOptions<KafkaTransportOptions> options, ILogger<KafkaTransport> log)
        {
            Options = options.Value;

            producer =
                new ProducerBuilder<Null, Null>(options.Value)
                    .SetLogHandler(KafkaLogFactory<Null, Null>.ProducerLog(log))
                    .SetErrorHandler(KafkaLogFactory<Null, Null>.ProducerError(log))
                    .SetStatisticsHandler(KafkaLogFactory<Null, Null>.ProducerStats(log))
                    .Build();

            var objectFactory = ActivatorUtilities.CreateFactory(typeof(KafkaTransport), new[] { typeof(string) });

            factory = name =>
            {
                return (ITransport)objectFactory(serviceProvider, new object[] { name });
            };
        }

        public ITransport GetTransport(string channelName)
        {
            return factory(channelName);
        }

        public void Dispose()
        {
            producer.Dispose();
        }
    }
}
