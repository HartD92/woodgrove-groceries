using Microsoft.Kiota.Abstractions;
using Woodgrove.Migration.Graph;
using Xunit;

namespace Woodgrove.Migration.Tests;

public class GraphRetryPolicyTests
{
    [Fact]
    public async Task ExecuteAsync_Retries429UsingRetryAfterHeader()
    {
        List<TimeSpan> delays = [];
        var policy = new GraphRetryPolicy(3, (delay, _) =>
        {
            delays.Add(delay);
            return Task.CompletedTask;
        });

        var attempts = 0;
        var result = await policy.ExecuteAsync<int>(_ =>
        {
            attempts++;
            if (attempts == 1)
            {
                throw CreateApiException(429, "5");
            }

            return Task.FromResult(42);
        });

        Assert.Equal(42, result);
        Assert.Equal(2, attempts);
        Assert.Single(delays);
        Assert.Equal(TimeSpan.FromSeconds(5), delays[0]);
    }

    private static ApiException CreateApiException(int statusCode, string retryAfter)
    {
        return new ApiException("throttled")
        {
            ResponseStatusCode = statusCode,
            ResponseHeaders = new Dictionary<string, IEnumerable<string>>
            {
                ["Retry-After"] = [retryAfter]
            }
        };
    }
}
