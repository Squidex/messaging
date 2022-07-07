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

namespace Squidex.Messaging.Implementation.GooglePubSub
{
    public sealed class GooglePubSubTransport : ITransport
    {
        private readonly GooglePubSubTransportOptions options;
        private readonly ILogger<GooglePubSubTransport> log;
        private readonly string topicId;
        private PublisherClient? publisherClient;
        private bool autoAck;

        public GooglePubSubTransport(string channelName,
            IOptions<GooglePubSubTransportOptions> options, ILogger<GooglePubSubTransport> log)
        {
            this.log = log;
            this.options = options.Value;
            this.topicId = $"{options.Value.Prefix}{channelName}";
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
            catch (RpcException ex) when (ex.Status.StatusCode != StatusCode.AlreadyExists)
            {
                throw;
            }

            var subcriptionName = new SubscriptionName(options.ProjectId, topicId);
            var subscriberClient = await SubscriberServiceApiClient.CreateAsync(ct);

            var timeoutInSec = (int)channelOptions.Timeout.TotalSeconds;

            if (timeoutInSec > 600)
            {
                autoAck = true;
            }

            try
            {
                await subscriberClient.CreateSubscriptionAsync(subcriptionName, topicName, new PushConfig(), autoAck ? 0 : timeoutInSec, ct);
            }
            catch (RpcException ex) when (ex.Status.StatusCode != StatusCode.AlreadyExists)
            {
                throw;
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
            CancellationToken ct = default)
        {
            if (publisherClient == null)
            {
                ThrowHelper.InvalidOperationException("Transport not initialized yet.");
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
            CancellationToken ct = default)
        {
            var subcriptionName = new SubscriptionName(options.ProjectId, topicId);
            var subscriberClient = await SubscriberClient.CreateAsync(subcriptionName);

            return new GooglePubSubSubscription(subscriberClient, callback, log);
        }
    }
}
