using NotificationApp.Domain.Interfaces;

namespace NotificationApp.IntegrationTests.Helpers;

/// <summary>Test double that always denies a rate-limit slot (simulates an exhausted limiter).</summary>
internal sealed class ExhaustedRateLimiter : IRateLimiter
{
    public bool TryAcquire() => false;
}
