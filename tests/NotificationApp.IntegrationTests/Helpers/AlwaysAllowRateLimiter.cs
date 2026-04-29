using NotificationApp.Domain.Interfaces;

namespace NotificationApp.IntegrationTests.Helpers;

/// <summary>Test double that always grants a rate-limit slot.</summary>
internal sealed class AlwaysAllowRateLimiter : IRateLimiter
{
    public bool TryAcquire() => true;
}
