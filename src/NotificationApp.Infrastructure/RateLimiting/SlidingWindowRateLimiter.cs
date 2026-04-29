using System.Collections.Concurrent;
using NotificationApp.Domain.Interfaces;

namespace NotificationApp.Infrastructure.RateLimiting;

public sealed class SlidingWindowRateLimiter : IRateLimiter
{
    private const int MaxRequests = 10;
    private static readonly TimeSpan Window = TimeSpan.FromMinutes(1);

    private readonly ConcurrentQueue<DateTime> _timestamps = new();
    private readonly object _lock = new();

    public bool TryAcquire()
    {
        lock (_lock)
        {
            var now    = DateTime.UtcNow;
            var cutoff = now - Window;

            while (_timestamps.TryPeek(out var oldest) && oldest < cutoff)
                _timestamps.TryDequeue(out _);

            if (_timestamps.Count >= MaxRequests)
                return false;

            _timestamps.Enqueue(now);
            return true;
        }
    }
}
