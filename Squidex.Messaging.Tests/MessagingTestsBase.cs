// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using System.Collections.Concurrent;
using FakeItEasy;
using Squidex.Hosting;
using Squidex.Messaging.Implementation;
using Xunit;

namespace Squidex.Messaging
{
    public abstract class MessagingTestsBase
    {
        private DateTime now = DateTime.UtcNow;

        protected abstract IServiceProvider CreateServices<T>(string channelName, IMessageHandler<T> handler, IClock clock);

        private class Message
        {
            public int Value { get; set; }
        }

        private async Task<(IAsyncDisposable, IMessageProducer<T>)> CreateMessagingAsync<T>(string channelName, IMessageHandler<T> handler)
        {
            var clock = A.Fake<IClock>();

            A.CallTo(() => clock.UtcNow)
                .ReturnsLazily(_ => now);

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

            public DelegatingHandler(Func<T, Task> action)
            {
                this.action = action;
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

            var consumer = new DelegatingHandler<Message>(message =>
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
                systems.Add(await CreateMessagingAsync(messageChannel, consumer));
            }

            try
            {
                foreach (var message in messagesSent)
                {
                    await systems[0].Producer.ProduceAsync(new Message { Value = message });
                }

                await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(30)));

                Assert.Equal(messagesReceives.OrderBy(x => x).ToList(), messagesSent);
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
        public async Task Should_bring_message_back_when_consumer_timesout()
        {
            var tcs = new TaskCompletionSource<bool>();

            var messageChannel = Guid.NewGuid().ToString();
            var messagesSent = Enumerable.Range(0, 20).ToList();
            var messagesReceives = new ConcurrentBag<int>();

            var consumer1 = new DelegatingHandler<Message>(message =>
            {
                return Task.Delay(10000000);
            });

            var (cleaner1, producer1) = await CreateMessagingAsync(messageChannel, consumer1);
            try
            {
                foreach (var message in messagesSent)
                {
                    await producer1.ProduceAsync(new Message { Value = message });
                }
            }
            finally
            {
                await cleaner1.DisposeAsync();
            }

            now = now.AddHours(2);

            var consumer2 = new DelegatingHandler<Message>(message =>
            {
                messagesReceives.Add(message.Value);

                if (messagesSent.Count == messagesReceives.Count)
                {
                    tcs.SetResult(true);
                }

                return Task.CompletedTask;
            });

            var (cleaner2, _) = await CreateMessagingAsync(messageChannel, consumer2);
            try
            {
                await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(30)));

                Assert.Equal(messagesReceives.OrderBy(x => x).ToList(), messagesSent);
            }
            finally
            {
                await cleaner2.DisposeAsync();
            }
        }
    }
}
