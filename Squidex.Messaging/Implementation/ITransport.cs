// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

#pragma warning disable MA0048 // File name must match type name

using Squidex.Hosting;

namespace Squidex.Messaging.Implementation
{
    public delegate Task MessageTransportCallback(TransportMessage transportMessage, IMessageAck ack,
            CancellationToken ct);

    public interface ITransport : IInitializable
    {
        Task<IAsyncDisposable> SubscribeAsync(MessageTransportCallback callback,
            CancellationToken ct = default);

        Task ProduceAsync(TransportMessage transportMessage,
            CancellationToken ct = default);

        void CleanupOldEntries(TimeSpan timeout, TimeSpan expires);
    }
}
