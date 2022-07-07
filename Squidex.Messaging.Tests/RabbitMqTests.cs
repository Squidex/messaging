// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

namespace Squidex.Messaging
{
    public class RabbitMqTests : MessagingTestsBase
    {
        public override string ChannelName => "dev";

        protected override void ConfigureServices(IServiceCollection services, string channelName)
        {
            services
                .AddRabbitMqTransport(TestHelpers.Configuration)
                .AddMessaging(channelName, true, options =>
                {
                    options.Expires = TimeSpan.FromDays(1);
                });
        }
    }
}
