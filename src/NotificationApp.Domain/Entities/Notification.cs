using NotificationApp.Domain.Enums;

namespace NotificationApp.Domain.Entities;

public sealed class Notification
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public NotificationLevel Level { get; init; }
    public string Message { get; init; } = string.Empty;
    public string? Source { get; init; }
    public DateTime Timestamp { get; init; } = DateTime.UtcNow;
}
