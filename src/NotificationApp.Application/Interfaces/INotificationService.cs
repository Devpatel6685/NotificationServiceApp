using NotificationApp.Application.DTOs;

namespace NotificationApp.Application.Interfaces;

public interface INotificationService
{
    Task<SendNotificationResponse> ProcessAsync(
        SendNotificationRequest request,
        CancellationToken cancellationToken = default);
}
