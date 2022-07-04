// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Squidex.Hosting;
using Squidex.Messaging;
using Squidex.Messaging.Implementation;
using Squidex.Messaging.Implementation.MongoDB;

namespace Microsoft.Extensions.DependencyInjection
{
    public static class MessagingServiceExtensions
    {
        public static IServiceCollection AddMessagingTransport(this IServiceCollection services, IConfiguration config)
        {
            config.ConfigureByOption("messaging:type", new Alternatives
            {
                ["MongoDb"] = () =>
                {
                    services.AddMongoDbTransport(config);
                }
            });

            return services;
        }

        public static IServiceCollection AddMongoDbTransport(this IServiceCollection services, IConfiguration config, Action<MongoDbTransportOptions>? configure = null)
        {
            services.Configure<MongoDbTransportOptions>(config, "messaging:mongoDb");

            if (configure != null)
            {
                services.Configure(configure);
            }

            services.AddSingletonAs<MongoDbTransportFactory>()
                .As<ITransportFactory>().AsSelf();

            return services;
        }

        public static IServiceCollection AddMessaging(this IServiceCollection services, Action<MessagingOptions>? configure = null)
        {
            services.Configure<MessagingOptions>(options =>
            {
                configure?.Invoke(options);
            });

            services.TryAddSingleton<ITransportSerializer,
                NewtonsoftJsonTransportSerializer>();

            services.TryAddSingleton<IMessageBus,
                DefaultMessageBus>();

            services.TryAddSingleton<IClock,
                DefaultClock>();

            services.TryAddSingleton<
                HandlerPipeline>();

            return services;
        }

        public static IServiceCollection AddMessaging(this IServiceCollection services, string channelName, Action<ChannelOptions>? configure = null)
        {
            services.Configure<ChannelOptions>(channelName, options =>
            {
                configure?.Invoke(options);
            });

            DelegatingProducer FindProducer(IServiceProvider sp)
            {
                return sp.GetRequiredService<IEnumerable<DelegatingProducer>>().Single(x => x.ChannelName == channelName);
            }

            DelegatingConsumer FindConsumer(IServiceProvider sp)
            {
                return sp.GetRequiredService<IEnumerable<DelegatingConsumer>>().Single(x => x.ChannelName == channelName);
            }

            AddMessaging(services);

            services.AddSingleton(
                sp => ActivatorUtilities.CreateInstance<DelegatingProducer>(sp, channelName));

            services.AddSingleton(
                sp => ActivatorUtilities.CreateInstance<DelegatingConsumer>(sp, channelName));

            services.AddSingleton<IInternalMessageProducer>(
                FindProducer);

            services.AddSingleton<IInitializable>(
                FindProducer);

            services.AddSingleton<IInitializable>(
                FindConsumer);

            services.AddSingleton<IBackgroundProcess>(
                FindConsumer);

            return services;
        }
    }
}
