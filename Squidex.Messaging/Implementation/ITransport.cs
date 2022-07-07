// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

#pragma warning disable MA0048 // File name must match type name

namespace Squidex.Messaging.Implementation
{
    public delegate Task MessageTransportCallback(TransportResult transportResult, IMessageAck ack,
            CancellationToken ct);

    public interface ITransport
    {
        Task InitializeAsync(ChannelOptions channelOptions,
            CancellationToken ct);

        Task ReleaseAsync(
            CancellationToken ct);

        Task<IAsyncDisposable> SubscribeAsync(MessageTransportCallback callback,
            CancellationToken ct = default);

        Task ProduceAsync(TransportMessage transportMessage,
            CancellationToken ct = default);

        void CleanupOldEntries(TimeSpan timeout, TimeSpan expires);
    }
}
