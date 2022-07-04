// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using System.Collections.Concurrent;
using System.Globalization;
using FakeItEasy;
using Squidex.Hosting;
using Squidex.Messaging.Implementation;
using Xunit;

namespace Squidex.Messaging
{
    public abstract class MessagingTestsBase
    {
        protected abstract IServiceProvider CreateServices<T>(string channelName, IMessageHandler<T> handler, IClock clock);

        private class Message
        {
            public int Value { get; set; }
        }

        private async Task<(IAsyncDisposable, IMessageProducer<T>)> CreateMessagingAsync<T>(string channelName, IMessageHandler<T> handler, DateTime now)
        {
            var clock = A.Fake<IClock>();

            A.CallTo(() => clock.UtcNow)
                .Returns(now);

            var serviceProvider = CreateServices(channelName, handler, clock);

            foreach (var initializable in serviceProvider.GetRequiredService<IEnumerable<IInitializable>>())
            {
                await initializable.InitializeAsync(default);
            }

            var producer = serviceProvider.GetRequiredService<IMessageProducer<T>>();

            return (new Cleanup(serviceProvider), producer);
        }

        private class Cleanup : IAsyncDisposable
        {
            private readonly IServiceProvider serviceProvider;

            public Cleanup(IServiceProvider serviceProvider)
            {
                this.serviceProvider = serviceProvider;
            }

            public async ValueTask DisposeAsync()
            {
                foreach (var initializable in serviceProvider.GetRequiredService<IEnumerable<IInitializable>>())
                {
                    await initializable.ReleaseAsync(default);
                }

                (serviceProvider as IDisposable)?.Dispose();
            }
        }

        private class DelegatingHandler<T> : IMessageHandler<T>
        {
            private readonly Func<T, Task> action;

            public string Name { get; }

            public DelegatingHandler(string name, Func<T, Task> action)
            {
                this.action = action;

                Name = name;
            }

            public Task HandleAsync(T message, CancellationToken ct = default)
            {
                return action(message);
            }
        }

        [Theory]
        [InlineData(1)]
        [InlineData(4)]
        [InlineData(20)]
        public async Task Should_consume_messages(int numConsumers)
        {
            var tcs = new TaskCompletionSource<bool>();

            var messageChannel = Guid.NewGuid().ToString();
            var messagesSent = Enumerable.Range(0, 20).ToList();
            var messagesReceives = new ConcurrentBag<int>();

            var consumer = new DelegatingHandler<Message>("Handle", message =>
            {
                messagesReceives.Add(message.Value);

                if (messagesSent.Count == messagesReceives.Count)
                {
                    tcs.SetResult(true);
                }

                return Task.CompletedTask;
            });

            var systems = new List<(IAsyncDisposable Cleaner, IMessageProducer<Message> Producer)>();

            for (var i = 0; i < numConsumers; i++)
            {
                systems.Add(await CreateMessagingAsync(messageChannel, consumer, DateTime.UtcNow));
            }

            try
            {
                foreach (var message in messagesSent)
                {
                    var key = message.ToString(CultureInfo.InvariantCulture);

                    await systems[0].Producer.ProduceAsync(new Message { Value = message }, key);
                }

                await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(30)));

                Assert.Equal(messagesSent, messagesReceives.OrderBy(x => x).ToList());
            }
            finally
            {
                foreach (var (cleaner, _) in systems)
                {
                    await cleaner.DisposeAsync();
                }
            }
        }

        [Fact]
        public async Task Should_bring_message_back_when_consumer_times_out()
        {
            var tcs = new TaskCompletionSource<bool>();

            var messageChannel = Guid.NewGuid().ToString();
            var messagesSent = Enumerable.Range(0, 20).ToList();
            var messagesReceives = new ConcurrentBag<int>();

            var consumer1 = new DelegatingHandler<Message>("Invalid", message =>
            {
                return Task.Delay(TimeSpan.FromDays(30));
            });

            var (cleaner1, producer1) = await CreateMessagingAsync(messageChannel, consumer1, DateTime.UtcNow);
            try
            {
                foreach (var message in messagesSent)
                {
                    var key = message.ToString(CultureInfo.InvariantCulture);

                    await producer1.ProduceAsync(new Message { Value = message }, key);
                }
            }
            finally
            {
                await cleaner1.DisposeAsync();
            }

            var consumer2 = new DelegatingHandler<Message>("Handle", message =>
            {
                messagesReceives.Add(message.Value);

                if (messagesSent.Count == messagesReceives.Count)
                {
                    tcs.SetResult(true);
                }

                return Task.CompletedTask;
            });

            var (cleaner2, _) = await CreateMessagingAsync(messageChannel, consumer2, DateTime.UtcNow.AddHours(1));
            try
            {
                await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(30)));

                Assert.Equal(messagesSent, messagesReceives.OrderBy(x => x).ToList());
            }
            finally
            {
                await cleaner2.DisposeAsync();
            }
        }
    }
}
