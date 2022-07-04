// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

#pragma warning disable RECS0096 // Type parameter is never used

namespace Squidex.Messaging
{
    public sealed class MessagingOptions<T>
    {
        public string ChannelName { get; set; }

        public int NumSubscriptions { get; set; } = 1;

        public int NumWorkers { get; set; } = 1;

        public TimeSpan Timeout { get; set; } = TimeSpan.FromHours(30);

        public TimeSpan Expires { get; set; } = TimeSpan.FromHours(1);
    }
}
