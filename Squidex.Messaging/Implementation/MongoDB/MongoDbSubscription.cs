// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using Microsoft.Extensions.Logging;
using MongoDB.Driver;

namespace Squidex.Messaging.Implementation.MongoDb
{
    internal sealed class MongoDbSubscription : IAsyncDisposable, IMessageAck
    {
        private static readonly UpdateDefinitionBuilder<MongoDbMessage> Update = Builders<MongoDbMessage>.Update;
        private readonly string channelName;
        private readonly IMongoCollection<MongoDbMessage> collection;
        private readonly MongoDbTransportOptions options;
        private readonly IClock clock;
        private readonly ILogger log;
        private readonly SimpleTimer timer;

        public MongoDbSubscription(string channelName, MessageTransportCallback callback, IMongoCollection<MongoDbMessage> collection,
            MongoDbTransportOptions options, IClock clock, ILogger log)
        {
            this.channelName = channelName;
            this.collection = collection;
            this.options = options;
            this.clock = clock;
            this.log = log;

            timer = new SimpleTimer(async ct =>
            {
                while (await PollMessageAsync(callback, ct))
                {
                    // If we have received a message it is very likely to fetch another one, so we loop until the queue is empty.
                }
            }, options.PollingInterval, log);
        }

        private Task<bool> PollMessageAsync(MessageTransportCallback callback,
            CancellationToken ct)
        {
            if (options.Prefetch <= 0)
            {
                return PollNormalAsync(callback, ct);
            }
            else
            {
                return PollPrefetchAsync(callback, ct);
            }
        }

        private async Task<bool> PollNormalAsync(MessageTransportCallback callback,
            CancellationToken ct)
        {
            var now = clock.UtcNow;

            // We can fetch an document in one go with this operation.
            var mongoMessage =
                await collection.FindOneAndUpdateAsync(x => x.TimeHandled == null && x.TimeToLive > now,
                    Update
                        .Set(x => x.TimeHandled, now),
                    cancellationToken: ct);

            if (mongoMessage == null || ct.IsCancellationRequested)
            {
                return false;
            }

            await callback(mongoMessage.ToTransportResult(), this, ct);
            return true;
        }

        private async Task<bool> PollPrefetchAsync(MessageTransportCallback callback,
            CancellationToken ct)
        {
            var now = clock.UtcNow;

            // There is no way to limit the updates, therefore we have to query candidates first.
            var candidates =
                await collection.Find(x => x.TimeHandled == null && x.TimeToLive > now)
                    .Limit(options.Prefetch)
                    .Project<MongoMessageId>(Builders<MongoDbMessage>.Projection.Include(x => x.Id))
                    .ToListAsync(ct);

            if (candidates.Count == 0 || ct.IsCancellationRequested)
            {
                return false;
            }

            var ids = candidates.Select(x => x.Id).ToList();

            // We cannot modify many documents at the same time and return them, therefore we try this approach.
            var updateId = Guid.NewGuid().ToString();

            var update =
                await collection.UpdateManyAsync(x => ids.Contains(x.Id) && x.PrefetchId == null,
                    Update
                        .Set(x => x.TimeHandled, now)
                        .Set(x => x.PrefetchId, updateId),
                    cancellationToken: ct);

            // If nothing has been updated, the documents have been fetched by another consumer.
            if (ct.IsCancellationRequested || (update.IsModifiedCountAvailable && update.ModifiedCount == 0))
            {
                return false;
            }

            // Get the documents that just have been updated.
            var mongoMessages =
                await collection.Find(x => x.PrefetchId == updateId)
                    .ToListAsync(ct);

            if (mongoMessages.Count == 0)
            {
                return false;
            }

            foreach (var mongoMessage in mongoMessages)
            {
                if (ct.IsCancellationRequested)
                {
                    return false;
                }

                await callback(mongoMessage.ToTransportResult(), this, ct);
            }

            return true;
        }

        public ValueTask DisposeAsync()
        {
            timer.Dispose();

            return default;
        }

        public async Task OnErrorAsync(TransportResult result,
            CancellationToken ct)
        {
            if (timer.IsDisposed)
            {
                return;
            }

            if (result.Data is not string id)
            {
                log.LogWarning("Transport message has no MongoDb ID.");
                return;
            }

            try
            {
                await collection.UpdateOneAsync(x => x.Id == id, Update.Set(x => x.TimeHandled, null), null, ct);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to put the message back into the queue '{queue}'.", channelName);
            }
        }

        public async Task OnSuccessAsync(TransportResult result,
            CancellationToken ct)
        {
            if (timer.IsDisposed)
            {
                return;
            }

            if (result.Data is not string id)
            {
                log.LogWarning("Transport message has no MongoDb ID.");
                return;
            }

            try
            {
                await collection.DeleteOneAsync(x => x.Id == id, ct);
            }
            catch (Exception ex)
            {
                log.LogError(ex, "Failed to remove message from queue '{queue}'.", channelName);
            }
        }
    }
}
