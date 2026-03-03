using Microsoft.Extensions.Logging;

namespace HotspotApp.Services.Infrastructure;

public sealed class RetryPolicy(ILogger<RetryPolicy> logger)
{
    public async Task ExecuteAsync(
        Func<CancellationToken, Task> action,
        CancellationToken token,
        int maxRetries = 10,
        int delaySeconds = 10
    )
    {
        int retryCount = 0;
        while (!token.IsCancellationRequested)
        {
            try
            {
                await action(token);
                return;
            }
            catch (OperationCanceledException)
            {
                break;
            }
            catch (Exception ex)
            {
                retryCount++;
                logger.LogError(
                    ex,
                    "Operation failed. Retry {RetryCount}/{MaxRetries}",
                    retryCount,
                    maxRetries
                );

                if (retryCount >= maxRetries && maxRetries != -1)
                {
                    logger.LogError("Max retries reached. Giving up.");
                    throw;
                }

                try
                {
                    await Task.Delay(TimeSpan.FromSeconds(delaySeconds), token);
                }
                catch (OperationCanceledException)
                {
                    break;
                }
            }
        }
    }
}
