// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

namespace Squidex.Messaging.Implementation
{
    internal sealed class DefaultClock : IClock
    {
        public DateTime UtcNow => DateTime.UtcNow;
    }
}
