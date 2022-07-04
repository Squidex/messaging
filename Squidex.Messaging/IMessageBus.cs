// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

namespace Squidex.Messaging
{
    public interface IMessageBus
    {
        Task PublishAsync(object message, string? key = null,
            CancellationToken ct = default);

        Task PublishToChannelAsync(object message, string channelName, string? key = null,
            CancellationToken ct = default);
    }
}
