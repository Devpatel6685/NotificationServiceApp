namespace NotificationApp.Application.DTOs;

public sealed record SendNotificationResponse(
    Guid NotificationId,
    NotificationStatus Status,
    string StatusMessage)
{
    public bool Dispatched => Status == NotificationStatus.Accepted;
}
