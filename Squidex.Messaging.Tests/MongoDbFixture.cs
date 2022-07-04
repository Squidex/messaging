// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using MongoDB.Driver;

namespace Squidex.Messaging
{
    public sealed class MongoDbFixture : IDisposable
    {
        public IMongoDatabase Database { get; }

        public MongoDbFixture()
        {
            var mongoClient = new MongoClient("mongodb://localhost");
            var mongoDatabase = mongoClient.GetDatabase("Messaging_Tests");

            Database = mongoDatabase;

            Dispose();
        }

        public void Dispose()
        {
            var collections = Database.ListCollectionNames().ToList();

            foreach (var collectionName in collections.Where(x => x.StartsWith("Queue", StringComparison.OrdinalIgnoreCase)))
            {
                Database.DropCollection(collectionName);
            }
        }
    }
}
