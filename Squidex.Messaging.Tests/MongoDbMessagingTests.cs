﻿// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using Squidex.Messaging.Implementation;
using Xunit;

#pragma warning disable SA1300 // Element should begin with upper-case letter

namespace Squidex.Messaging
{
    public class MongoDbMessagingTests : MessagingTestsBase, IClassFixture<MongoDbFixture>
    {
        public MongoDbFixture _ { get; }

        public MongoDbMessagingTests(MongoDbFixture fixture)
        {
            _ = fixture;
        }

        protected override IServiceProvider CreateServices<T>(string channelName, IMessageHandler<T> handler, IClock clock)
        {
            var services =
                new ServiceCollection()
                    .AddSingleton(_.Database)
                    .AddLogging()
                    .AddSingleton(clock)
                    .AddSingleton(handler)
                    .AddMongoDbTransport(TestHelpers.Configuration)
                    .AddMessaging<T>(channelName, options =>
                    {
                        options.Expires = TimeSpan.FromDays(1);
                    })
                    .BuildServiceProvider();

            return services;
        }
    }
}
