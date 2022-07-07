// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

namespace Squidex.Messaging
{
    public class GoogleCloudTests : MessagingTestsBase
    {
        public override string ChannelName => "messaging-tests";

        protected override void ConfigureServices(IServiceCollection services, string channelName)
        {
            services
                .AddGooglePubSubTransport(TestHelpers.Configuration)
                .AddMessaging(channelName, true, options =>
                {
                    options.Expires = TimeSpan.FromDays(1);
                });
        }
    }
}
