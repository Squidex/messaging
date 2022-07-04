// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using MongoDB.Driver;

namespace Squidex.Messaging.Implementation.MongoDB
{
    public sealed class MongoDbTransport : ITransport
    {
        private static readonly UpdateDefinitionBuilder<MongoDbMessage> Update = Builders<MongoDbMessage>.Update;
        private readonly IMongoCollection<MongoDbMessage> collection;
        private readonly MongoDbTransportOptions options;
        private readonly IClock clock;
        private readonly ILogger<MongoDbTransport> log;
        private SimpleTimer? updateTimer;
        private bool isInitialized;
        private bool isReleased;

        public MongoDbTransport(IMongoDatabase database, string channelName,
            IOptions<MongoDbTransportOptions> options, IClock clock, ILogger<MongoDbTransport> log)
        {
            this.options = options.Value;
            this.clock = clock;
            this.log = log;

            collection = database.GetCollection<MongoDbMessage>($"{options.Value.CollectionName}_{channelName}");
        }

        public Task InitializeAsync(
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
                            .Ascending(x => x.IsHandled)),
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

        public void CleanupOldEntries(TimeSpan timeout)
        {
            if (updateTimer != null)
            {
                return;
            }

            updateTimer = new SimpleTimer(async ct =>
            {
                var now = clock.UtcNow;

                await collection.UpdateManyAsync(x => x.IsHandled && x.TimeToRetry < now,
                    Update
                        .Set(x => x.IsHandled, false)
                        .Set(x => x.TimeToRetry, now + timeout),
                    cancellationToken: ct);
            }, options.UpdateInterval, log);
        }

        public Task ProduceAsync(TransportMessage transportMessage,
            CancellationToken ct = default)
        {
            var request = new MongoDbMessage
            {
                Id = Guid.NewGuid().ToString(),
                MessageBody = transportMessage.Data,
                MessageHeaders = transportMessage.Headers?.ToDictionary(x => x.Key, x => x.Value),
                TimeToLive = GetTimeToLive(transportMessage.Headers),
                TimeToRetry = GetTimeToRetry(transportMessage.Headers)
            };

            return collection.InsertOneAsync(request, null, ct);
        }

        private DateTime GetTimeToLive(IReadOnlyDictionary<string, string>? headers)
        {
            var time = TimeSpan.FromDays(30);

            if (headers != null && headers.TryGetValue(Headers.TimeExpires, out var timeToBeReceivedString))
            {
                if (TimeSpan.TryParse(timeToBeReceivedString, CultureInfo.InvariantCulture, out var parsed))
                {
                    time = parsed;
                }
            }

            return clock.UtcNow + time;
        }

        private DateTime GetTimeToRetry(IReadOnlyDictionary<string, string>? headers)
        {
            var time = TimeSpan.FromDays(10000);

            if (headers != null && headers.TryGetValue(Headers.TimeRetry, out var timeToBeReceivedString))
            {
                if (TimeSpan.TryParse(timeToBeReceivedString, CultureInfo.InvariantCulture, out var parsed))
                {
                    time = parsed;
                }
            }

            return clock.UtcNow + time;
        }

        public Task<IAsyncDisposable> SubscribeAsync(MessageTransportCallback callback, CancellationToken ct = default)
        {
            var subscription = new MongoDbSubscription(callback, collection, options, log);

            return Task.FromResult<IAsyncDisposable>(subscription);
        }
    }
}
