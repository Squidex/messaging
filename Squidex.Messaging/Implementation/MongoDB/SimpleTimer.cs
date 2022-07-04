// ==========================================================================
//  Squidex Headless CMS
// ==========================================================================
//  Copyright (c) Squidex UG (haftungsbeschraenkt)
//  All rights reserved. Licensed under the MIT license.
// ==========================================================================

using Microsoft.Extensions.Logging;

namespace Squidex.Messaging.Implementation.MongoDB
{
    internal sealed class SimpleTimer : IDisposable
    {
        private readonly CancellationTokenSource stopToken = new CancellationTokenSource();

        public bool IsDisposed => stopToken.IsCancellationRequested;

        public SimpleTimer(Func<CancellationToken, Task> action, TimeSpan interval, ILogger log)
        {
            Task.Run(async () =>
            {
                try
                {
                    while (!stopToken.IsCancellationRequested)
                    {
                        try
                        {
                            await action(stopToken.Token);

                            await Task.Delay(interval, stopToken.Token);
                        }
                        catch (OperationCanceledException)
                        {
                        }
                        catch (Exception ex)
                        {
                            log.LogWarning(ex, "Failed to poll from queue.");
                        }
                    }
                }
                catch
                {
                    return;
                }
            }, stopToken.Token);
        }

        public void Dispose()
        {
            stopToken.Cancel();
        }
    }
}
