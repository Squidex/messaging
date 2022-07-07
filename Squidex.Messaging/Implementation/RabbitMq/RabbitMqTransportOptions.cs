// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using Squidex.Hosting.Configuration;

namespace Squidex.Messaging.Implementation.RabbitMq
{
    public sealed class RabbitMqTransportOptions : IValidatableOptions
    {
        public Uri Uri { get; set; }

        public int MaxDegreeOfParallelism { get; set; } = Environment.ProcessorCount;

        public IEnumerable<ConfigurationError> Validate()
        {
            if (Uri == null)
            {
                yield return new ConfigurationError("Value is required.", nameof(Uri));
            }
        }
    }
}
