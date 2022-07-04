// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

namespace Squidex.Messaging
{
    public interface IMessageProducer<T>
    {
        Task ProduceAsync(T message, string? key = null,
            CancellationToken ct = default);
    }
}
