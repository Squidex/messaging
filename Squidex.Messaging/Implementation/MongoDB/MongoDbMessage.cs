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

        [BsonElement("b")]
        public byte[] MessageBody { get; init; }

        [BsonElement("h")]
        public Dictionary<string, string>? MessageHeaders { get; init; }

        [BsonElement("ttl")]
        public DateTime TimeToLive { get; init; }

        [BsonElement("ttr")]
        public DateTime TimeToRetry { get; init; }

        [BsonElement("pf")]
        public string PrefetchId { get; set; }

        [BsonElement("p")]
        public bool IsHandled { get; init; }

        public TransportMessage ToTransportMessage()
        {
            return new TransportMessage(MessageBody)
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
