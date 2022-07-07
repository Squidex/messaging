// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using RabbitMQ.Client;

namespace Squidex.Messaging.Implementation.RabbitMq
{
    public sealed class RabbitMqTransportFactory : ITransportFactory
    {
        private readonly Func<string, ITransport> factory;

        public IConnection Connection { get; }

        public RabbitMqTransportOptions Options { get; }

        public RabbitMqTransportFactory(IOptions<RabbitMqTransportOptions> options, IServiceProvider serviceProvider)
        {
            Options = options.Value;

            var connectionFactory = new ConnectionFactory
            {
                Uri = options.Value.Uri,

                // Of course we want an asynchronous behavior.
                DispatchConsumersAsync = true
            };

            Connection = connectionFactory.CreateConnection();

            var objectFactory = ActivatorUtilities.CreateFactory(typeof(RabbitMqTransport), new[] { typeof(string) });

            factory = name =>
            {
                return (ITransport)objectFactory(serviceProvider, new object[] { name });
            };
        }

        public ITransport GetTransport(string channelName)
        {
            return factory(channelName);
        }
    }
}
