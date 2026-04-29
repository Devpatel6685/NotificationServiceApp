namespace NotificationApp.Domain.Interfaces;

public interface IDiscordService
{
    Task SendAsync(string message, CancellationToken cancellationToken = default);
}
