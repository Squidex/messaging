// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using MongoDB.Bson.Serialization.Attributes;

#pragma warning disable MA0048 // File name must match type name

namespace Squidex.Messaging.Implementation.MongoDB
{
    internal sealed class MongoDbMessage
    {
        public string Id { get; init; }

        public byte[] MessageData { get; init; }

        public Dictionary<string, string> MessageHeaders { get; init; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime TimeToLive { get; init; }

        [BsonDateTimeOptions(Kind = DateTimeKind.Utc)]
        public DateTime? TimeHandled { get; init; }

        public string? PrefetchId { get; set; }

        public TransportMessage ToTransportMessage()
        {
            return new TransportMessage(MessageData)
            {
                Headers = MessageHeaders
            };
        }
    }

    internal sealed class MongoMessageId
    {
        public string Id { get; init; }
    }
}
