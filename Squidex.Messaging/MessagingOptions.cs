// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

namespace Squidex.Messaging
{
    public sealed class MessagingOptions
    {
        public RoutingCollection Routing { get; set; } = new RoutingCollection();
    }
}
