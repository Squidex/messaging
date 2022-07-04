// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

#pragma warning disable SA1313 // Parameter names should begin with lower-case letter

namespace Squidex.Messaging.Implementation
{
    public sealed record TransportMessage(byte[] Data)
    {
        public string? Key { get; init; }

        public DateTime Expires { get; init; }

        public DateTime Created { get; init; }

        public IReadOnlyDictionary<string, string>? Headers { get; init; }
    }
}
