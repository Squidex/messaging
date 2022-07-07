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
        Task OnSuccessAsync(TransportResult result,
            CancellationToken ct);

        Task OnErrorAsync(TransportResult result,
            CancellationToken ct);
    }
}
