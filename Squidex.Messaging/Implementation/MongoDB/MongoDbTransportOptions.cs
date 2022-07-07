// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

namespace Squidex.Messaging.Implementation.MongoDb
{
    public sealed class MongoDbTransportOptions
    {
        public string CollectionName { get; set; } = "Queue";

        public int Prefetch { get; set; }

        public TimeSpan UpdateInterval { get; set; } = TimeSpan.FromSeconds(5);

        public TimeSpan PollingInterval { get; set; } = TimeSpan.FromSeconds(5);
    }
}
