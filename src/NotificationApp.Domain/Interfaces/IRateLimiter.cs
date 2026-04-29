namespace NotificationApp.Domain.Interfaces;

public interface IRateLimiter
{
    bool TryAcquire();
}
