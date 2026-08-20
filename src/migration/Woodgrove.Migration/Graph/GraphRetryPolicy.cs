using Microsoft.Kiota.Abstractions;
using System.Net;

namespace Woodgrove.Migration.Graph;

public sealed class GraphRetryPolicy
{
    private readonly int _maxRetryAttempts;
    private readonly Func<TimeSpan, CancellationToken, Task> _delay;

    public GraphRetryPolicy(int maxRetryAttempts, Func<TimeSpan, CancellationToken, Task>? delay = null)
    {
        _maxRetryAttempts = Math.Max(1, maxRetryAttempts);
        _delay = delay ?? Task.Delay;
    }

    public async Task<T> ExecuteAsync<T>(Func<CancellationToken, Task<T>> operation, CancellationToken ct = default)
    {
        for (var attempt = 1; ; attempt++)
        {
            try
            {
                return await operation(ct).ConfigureAwait(false);
            }
            catch (ApiException ex) when (ex.ResponseStatusCode == (int)HttpStatusCode.TooManyRequests && attempt < _maxRetryAttempts)
            {
                await _delay(GetDelay(ex, attempt), ct).ConfigureAwait(false);
            }
        }
    }

    public async Task ExecuteAsync(Func<CancellationToken, Task> operation, CancellationToken ct = default)
    {
        await ExecuteAsync(async token =>
        {
            await operation(token).ConfigureAwait(false);
            return true;
        }, ct).ConfigureAwait(false);
    }

    private static TimeSpan GetDelay(ApiException exception, int attempt)
    {
        if (exception.ResponseHeaders is not null)
        {
            foreach (var header in exception.ResponseHeaders)
            {
                if (string.Equals(header.Key, "Retry-After", StringComparison.OrdinalIgnoreCase) &&
                    int.TryParse(header.Value?.FirstOrDefault(), out var seconds) &&
                    seconds > 0)
                {
                    return TimeSpan.FromSeconds(seconds);
                }
            }
        }

        return TimeSpan.FromSeconds(Math.Min(Math.Pow(2, attempt), 10));
    }
}
