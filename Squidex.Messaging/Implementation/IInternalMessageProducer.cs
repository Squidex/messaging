// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using Squidex.Hosting;

namespace Squidex.Messaging.Implementation
{
    public interface IInternalMessageProducer : IInitializable
    {
        string ChannelName { get; }

        Task ProduceAsync(object message, string? key = null,
            CancellationToken ct = default);
    }
}
