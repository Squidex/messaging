// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

namespace Squidex.Messaging
{
    public sealed class RoutingCollection : List<(Func<object, bool> Predicate, string ChannelName)>
    {
        public void Add(Func<object, bool> predicate, string channelName)
        {
            Add((predicate, channelName));
        }

        public void AddFallback(string channelName)
        {
            Add((x => true, channelName));
        }
    }
}
