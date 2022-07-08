// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using Xunit;

#pragma warning disable SA1300 // Element should begin with upper-case letter

namespace Squidex.Messaging
{
    public class MongoDbMessagingPrefetchTests : MessagingTestsBase, IClassFixture<MongoDbFixture>
    {
        public MongoDbFixture _ { get; }

        public MongoDbMessagingPrefetchTests(MongoDbFixture fixture)
        {
            _ = fixture;
        }

        protected override void ConfigureServices(IServiceCollection services, string channelName)
        {
            services
                .AddSingleton(_.Database)
                .AddMongoDbTransport(TestHelpers.Configuration, options =>
                {
                    options.Prefetch = 5;
                    options.PollingInterval = TimeSpan.FromSeconds(0.1);
                    options.UpdateInterval = TimeSpan.FromSeconds(0.1);
                })
                .AddMessaging(channelName, true, options =>
                {
                    options.Expires = TimeSpan.FromDays(1);
                });
        }
    }
}
