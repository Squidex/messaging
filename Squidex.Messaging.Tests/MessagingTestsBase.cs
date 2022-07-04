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
        protected abstract void ConfigureServices(IServiceCollection services, string channelName);

        private sealed class Message : BaseMessage
        {
            public int Value { get; set; }
        }

        private abstract class BaseMessage
        {
        }

        private async Task<(IAsyncDisposable, IMessageBus)> CreateMessagingAsync(string channelName, IMessageHandler handler, DateTime now,
            Action<MessagingOptions>? configure = null)
        {
            var clock = A.Fake<IClock>();

            A.CallTo(() => clock.UtcNow)
                .Returns(now);

            var servicerCollection =
                new ServiceCollection()
                    .AddLogging()
                    .AddSingleton(clock)
                    .AddSingleton(handler)
                    .AddMessaging(options =>
                    {
                        options.Routing.AddFallback(channelName);

                        configure?.Invoke(options);
                    });

            ConfigureServices(servicerCollection, channelName);

            var serviceProvider = servicerCollection.BuildServiceProvider();

            foreach (var initializable in serviceProvider.GetRequiredService<IEnumerable<IInitializable>>())
            {
                await initializable.InitializeAsync(default);
            }

            foreach (var process in serviceProvider.GetRequiredService<IEnumerable<IBackgroundProcess>>())
            {
                await process.StartAsync(default);
            }

            var producer = serviceProvider.GetRequiredService<IMessageBus>();

            return (new Cleanup(serviceProvider), producer);
        }

        private sealed class Cleanup : IAsyncDisposable
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

        [Fact]
        public async Task Should_throw_exception_if_no_route_not_valid()
        {
            var messageChannel = Guid.NewGuid().ToString();

            var consumer = new DelegatingHandler<Message>(message =>
            {
                return Task.CompletedTask;
            });

            var (app, bus) = await CreateMessagingAsync(messageChannel, consumer, DateTime.UtcNow, options =>
            {
                options.Routing.Clear();
                options.Routing.AddFallback("invalid");
            });

            await using (app)
            {
                await Assert.ThrowsAnyAsync<Exception>(() => bus.PublishAsync(213));
            }
        }

        [Fact]
        public async Task Should_throw_exception_if_no_route_found()
        {
            var messageChannel = Guid.NewGuid().ToString();

            var consumer = new DelegatingHandler<Message>(message =>
            {
                return Task.CompletedTask;
            });

            var (app, bus) = await CreateMessagingAsync(messageChannel, consumer, DateTime.UtcNow, options =>
            {
                options.Routing.Clear();
            });

            await using (app)
            {
                await Assert.ThrowsAnyAsync<Exception>(() => bus.PublishAsync(213));
            }
        }

        [Fact]
        public async Task Should_throw_exception_if_no_channel_found()
        {
            var messageChannel = Guid.NewGuid().ToString();

            var consumer = new DelegatingHandler<Message>(message =>
            {
                return Task.CompletedTask;
            });

            var (app, bus) = await CreateMessagingAsync(messageChannel, consumer, DateTime.UtcNow, options =>
            {
                options.Routing.Clear();
            });

            await using (app)
            {
                var message = new Message { Value = 1 };

                await Assert.ThrowsAnyAsync<Exception>(() => bus.PublishToChannelAsync(message, "invalid"));
            }
        }

        [Fact]
        public async Task Should_consume_base_classes()
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

            var (app, bus) = await CreateMessagingAsync(messageChannel, consumer, DateTime.UtcNow);

            await using (app)
            {
                foreach (var message in messagesSent)
                {
                    var key = message.ToString(CultureInfo.InvariantCulture);

                    await bus.PublishAsync(new Message { Value = message } as BaseMessage, key);
                }

                await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(30)));

                Assert.Equal(messagesSent, messagesReceives.OrderBy(x => x).ToList());
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

            var apps = new List<(IAsyncDisposable App, IMessageBus Bus)>();

            for (var i = 0; i < numConsumers; i++)
            {
                apps.Add(await CreateMessagingAsync(messageChannel, consumer, DateTime.UtcNow));
            }

            try
            {
                var bus = apps[0].Bus;

                foreach (var message in messagesSent)
                {
                    var key = message.ToString(CultureInfo.InvariantCulture);

                    await bus.PublishAsync(new Message { Value = message }, key);
                }

                await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(30)));

                Assert.Equal(messagesSent, messagesReceives.OrderBy(x => x).ToList());
            }
            finally
            {
                foreach (var (cleaner, _) in apps)
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

            var consumer1 = new DelegatingHandler<Message>(message =>
            {
                return Task.Delay(TimeSpan.FromDays(30));
            });

            var (app1, bus1) = await CreateMessagingAsync(messageChannel, consumer1, DateTime.UtcNow);

            await using (app1)
            {
                foreach (var message in messagesSent)
                {
                    var key = message.ToString(CultureInfo.InvariantCulture);

                    await bus1.PublishAsync(new Message { Value = message }, key);
                }
            }

            var consumer2 = new DelegatingHandler<Message>(message =>
            {
                messagesReceives.Add(message.Value);

                if (messagesSent.Count == messagesReceives.Count)
                {
                    tcs.SetResult(true);
                }

                return Task.CompletedTask;
            });

            var (app2, _) = await CreateMessagingAsync(messageChannel, consumer2, DateTime.UtcNow.AddHours(1));

            await using (app2)
            {
                await Task.WhenAny(tcs.Task, Task.Delay(TimeSpan.FromSeconds(30)));

                Assert.Equal(messagesSent, messagesReceives.OrderBy(x => x).ToList());
            }
        }
    }
}
