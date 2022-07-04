// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection.Extensions;
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

        public static IServiceCollection AddMessaging<T>(this IServiceCollection services, string channelName, Action<MessagingOptions<T>>? configure = null)
        {
            services.Configure<MessagingOptions<T>>(options =>
            {
                configure?.Invoke(options);

                options.ChannelName = channelName;
            });

            services.TryAddSingleton<ITransportSerializer,
                NewtonsoftJsonTransportSerializer>();

            services.TryAddSingleton<IClock,
                DefaultClock>();

            services.AddSingletonAs<DelegatingProducer<T>>()
                .As<IMessageProducer<T>>();

            services.AddSingletonAs<DelegatingConsumer<T>>()
                .AsSelf();

            return services;
        }
    }
}
