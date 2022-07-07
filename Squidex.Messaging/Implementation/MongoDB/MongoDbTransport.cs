// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Squidex.Messaging.Implementation.MongoDb
{
    public sealed class MongoDbTransport : ITransport
    {
        private static readonly UpdateDefinitionBuilder<MongoDbMessage> Update = Builders<MongoDbMessage>.Update;
        private readonly IMongoCollection<MongoDbMessage> collection;
        private readonly MongoDbTransportOptions options;
        private readonly string channelName;
        private readonly IClock clock;
        private readonly ILogger<MongoDbTransport> log;
        private SimpleTimer? updateTimer;
        private bool isInitialized;
        private bool isReleased;

        public MongoDbTransport(IMongoDatabase database, string channelName,
            IOptions<MongoDbTransportOptions> options, IClock clock, ILogger<MongoDbTransport> log)
        {
            this.options = options.Value;
            this.channelName = channelName;
            this.clock = clock;
            this.log = log;

            collection = database.GetCollection<MongoDbMessage>($"{options.Value.CollectionName}_{channelName}");
        }

        public Task InitializeAsync(ChannelOptions channelOptions,
            CancellationToken ct)
        {
            if (isInitialized)
            {
                return Task.CompletedTask;
            }

            isInitialized = true;

            return collection.Indexes.CreateManyAsync(
                new[]
                {
                    new CreateIndexModel<MongoDbMessage>(
                        Builders<MongoDbMessage>.IndexKeys
                            .Ascending(x => x.TimeHandled)),
                    new CreateIndexModel<MongoDbMessage>(
                        Builders<MongoDbMessage>.IndexKeys
                            .Ascending(x => x.TimeToLive),
                        new CreateIndexOptions
                        {
                            ExpireAfter = TimeSpan.Zero,
                        })
                },
                ct);
        }

        public Task ReleaseAsync(
           CancellationToken ct)
        {
            if (isReleased)
            {
                return Task.CompletedTask;
            }

            isReleased = true;

            updateTimer?.Dispose();

            return Task.CompletedTask;
        }

        public void CleanupOldEntries(TimeSpan timeout, TimeSpan expires)
        {
            if (updateTimer != null)
            {
                return;
            }

            updateTimer = new SimpleTimer(async ct =>
            {
                var now = clock.UtcNow;

                var timedout = now - timeout;

                var update = await collection.UpdateManyAsync(x => x.TimeHandled != null && x.TimeHandled < timedout,
                    Update
                        .Set(x => x.TimeHandled, null)
                        .Set(x => x.TimeToLive, now + expires)
                        .Set(x => x.PrefetchId, null),
                    cancellationToken: ct);

                if (update.IsModifiedCountAvailable && update.ModifiedCount > 0)
                {
                    log.LogInformation("{channelName}: Items reset: {count}.", channelName, update.ModifiedCount);
                }
            }, options.UpdateInterval, log);
        }

        public Task ProduceAsync(TransportMessage transportMessage,
            CancellationToken ct)
        {
            var request = new MongoDbMessage
            {
                Id = transportMessage.Headers[HeaderNames.Id],
                MessageData = transportMessage.Data,
                MessageHeaders = transportMessage.Headers,
                TimeToLive = GetTimeToLive(transportMessage.Headers),
            };

            return collection.InsertOneAsync(request, null, ct);
        }

        private DateTime GetTimeToLive(TransportHeaders headers)
        {
            var time = TimeSpan.FromDays(30);

            if (headers.TryGetTimestamp(HeaderNames.TimeExpires, out var expires))
            {
                time = expires;
            }

            return clock.UtcNow + time;
        }

        public Task<IAsyncDisposable> SubscribeAsync(MessageTransportCallback callback,
            CancellationToken ct)
        {
            var subscription = new MongoDbSubscription(channelName, callback, collection, options, clock, log);

            return Task.FromResult<IAsyncDisposable>(subscription);
        }
    }
}
