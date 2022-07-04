// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

namespace Squidex.Messaging
{
    public sealed class ChannelOptions
    {
        public int NumSubscriptions { get; set; } = 1;

        public int NumWorkers { get; set; } = 1;

        public TimeSpan Timeout { get; set; } = TimeSpan.FromMinutes(30);

        public TimeSpan Expires { get; set; } = TimeSpan.FromHours(1);
    }
}
