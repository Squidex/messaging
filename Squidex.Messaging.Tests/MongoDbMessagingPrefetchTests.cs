// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using MongoDB.Driver;
using Squidex.Messaging.Implementation;

namespace Squidex.Messaging
{
    public class MongoDbMessagingPrefetchTests : MessagingTestsBase
    {
        protected override IServiceProvider CreateServices<T>(string channelName, IMessageHandler<T> handler, IClock clock)
        {
            var mongoClient = new MongoClient("mongodb://localhost");
            var mongoDatabase = mongoClient.GetDatabase("Messaging_Tests");

            var services =
                new ServiceCollection()
                    .AddLogging()
                    .AddSingleton(clock)
                    .AddSingleton(handler)
                    .AddSingleton(mongoDatabase)
                    .AddMongoDbTransport(TestHelpers.Configuration, x => x.Prefetch = 100)
                    .AddMessaging<T>(channelName)
                    .BuildServiceProvider();

            return services;
        }
    }
}
