// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

namespace Squidex.Messaging.Implementation
{
    public interface IMessageAck
    {
        Task OnSuccessAsync(TransportMessage message,
            CancellationToken ct = default);

        Task OnErrorAsync(TransportMessage message,
            CancellationToken ct = default);
    }
}
