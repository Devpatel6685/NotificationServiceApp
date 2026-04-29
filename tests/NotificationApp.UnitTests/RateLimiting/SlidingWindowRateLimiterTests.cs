using FluentAssertions;
using NotificationApp.Infrastructure.RateLimiting;
using Xunit;

namespace NotificationApp.UnitTests.RateLimiting;

public sealed class SlidingWindowRateLimiterTests
{
    [Fact]
    public void TryAcquire_FirstRequest_ReturnsTrue()
    {
        var limiter = new SlidingWindowRateLimiter();

        limiter.TryAcquire().Should().BeTrue();
    }

    [Fact]
    public void TryAcquire_First10Requests_AllReturnTrue()
    {
        var limiter = new SlidingWindowRateLimiter();

        for (var i = 0; i < 10; i++)
            limiter.TryAcquire().Should().BeTrue($"request #{i + 1} should be within limit");
    }

    [Fact]
    public void TryAcquire_11thRequest_ReturnsFalse()
    {
        var limiter = new SlidingWindowRateLimiter();

        for (var i = 0; i < 10; i++)
            limiter.TryAcquire();

        limiter.TryAcquire().Should().BeFalse("the 11th request exceeds the 10/minute limit");
    }

    [Fact]
    public void TryAcquire_ExactlyAtLimit_ReturnsFalse()
    {
        var limiter = new SlidingWindowRateLimiter();

        // Consume all 10 slots
        for (var i = 0; i < 10; i++)
            limiter.TryAcquire();

        // Next attempt must fail
        limiter.TryAcquire().Should().BeFalse();
    }

    [Fact]
    public void TryAcquire_ConcurrentAccess_NeverExceedsLimit()
    {
        var limiter  = new SlidingWindowRateLimiter();
        var accepted = 0;

        Parallel.For(0, 20, _ =>
        {
            if (limiter.TryAcquire())
                Interlocked.Increment(ref accepted);
        });

        accepted.Should().BeLessOrEqualTo(10, "limit is 10 regardless of concurrency");
    }
}
