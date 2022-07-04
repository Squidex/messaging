// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using Microsoft.Extensions.DependencyInjection;

namespace Squidex.Messaging.Implementation.MongoDB
{
    public sealed class MongoDbTransportFactory : ITransportFactory
    {
        private readonly Func<string, ITransport> factory;

        public MongoDbTransportFactory(IServiceProvider serviceProvider)
        {
            var objectFactory = ActivatorUtilities.CreateFactory(typeof(MongoDbTransport), new[] { typeof(string) });

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
