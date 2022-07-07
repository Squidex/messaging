// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using Google.Cloud.PubSub.V1;
using Google.Protobuf;
using Grpc.Core;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Squidex.Messaging.Internal;
using GooglePushConfig = Google.Cloud.PubSub.V1.PushConfig;

namespace Squidex.Messaging.Implementation.GooglePubSub
{
    public sealed class GooglePubSubTransport : ITransport
    {
        private readonly GooglePubSubTransportOptions options;
        private readonly GooglePushConfig pushConfig = new GooglePushConfig();
        private readonly ILogger<GooglePubSubTransport> log;
        private readonly string topicId;
        private PublisherClient? publisherClient;

        public GooglePubSubTransport(string channelName,
            IOptions<GooglePubSubTransportOptions> options,
            ILogger<GooglePubSubTransport> log)
        {
            this.options = options.Value;
            this.log = log;

            topicId = $"{options.Value.Prefix}{channelName}";
        }

        public async Task InitializeAsync(ChannelOptions channelOptions,
            CancellationToken ct)
        {
            if (publisherClient != null)
            {
                return;
            }

            var topicName = new TopicName(options.ProjectId, $"{options.Prefix}{topicId}");
            var topicClient = await PublisherServiceApiClient.CreateAsync(ct);

            try
            {
                await topicClient.CreateTopicAsync(topicName, ct);
            }
            catch (RpcException ex) when (ex.Status.StatusCode == StatusCode.AlreadyExists)
            {
                // This exception is expected.
            }

            var subcriptionName = new SubscriptionName(options.ProjectId, topicId);
            var subscriberClient = await SubscriberServiceApiClient.CreateAsync(ct);

            var timeoutInSec = (int)Math.Min(channelOptions.Timeout.TotalSeconds, 600);

            try
            {
                await subscriberClient.CreateSubscriptionAsync(subcriptionName, topicName, pushConfig, timeoutInSec, ct);
            }
            catch (RpcException ex) when (ex.Status.StatusCode == StatusCode.AlreadyExists)
            {
                // This exception is expected.
            }

            publisherClient = await PublisherClient.CreateAsync(topicName);
        }

        public async Task ReleaseAsync(
            CancellationToken ct)
        {
            if (publisherClient == null)
            {
                return;
            }

            await publisherClient.ShutdownAsync(ct);

            publisherClient = null;
        }

        public void CleanupOldEntries(TimeSpan timeout, TimeSpan expires)
        {
        }

        public async Task ProduceAsync(TransportMessage transportMessage,
            CancellationToken ct)
        {
            if (publisherClient == null)
            {
                ThrowHelper.InvalidOperationException("Transport has not been initialized yet.");
                return;
            }

            var pubSubMessage = new PubsubMessage
            {
                Data = ByteString.CopyFrom(transportMessage.Data)
            };

            foreach (var (key, value) in transportMessage.Headers)
            {
                pubSubMessage.Attributes[key] = value;
            }

            await publisherClient.PublishAsync(pubSubMessage);
        }

        public async Task<IAsyncDisposable> SubscribeAsync(MessageTransportCallback callback,
            CancellationToken ct)
        {
            var subcriptionName = new SubscriptionName(options.ProjectId, topicId);
            var subscriberClient = await SubscriberClient.CreateAsync(subcriptionName);

            return new GooglePubSubSubscription(subscriberClient, callback, log);
        }
    }
}
