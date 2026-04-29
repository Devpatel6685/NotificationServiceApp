using NotificationApp.Application.DTOs;
using NotificationApp.Application.Interfaces;
using NotificationApp.Domain.Entities;
using NotificationApp.Domain.Enums;
using NotificationApp.Domain.Interfaces;

namespace NotificationApp.Application.Services;

public sealed class NotificationService : INotificationService
{
    private const NotificationLevel DispatchThreshold = NotificationLevel.Warning;

    private readonly IDiscordService _discordService;
    private readonly IRateLimiter _rateLimiter;

    public NotificationService(IDiscordService discordService, IRateLimiter rateLimiter)
    {
        _discordService = discordService;
        _rateLimiter = rateLimiter;
    }

    public async Task<SendNotificationResponse> ProcessAsync(
        SendNotificationRequest request,
        CancellationToken cancellationToken = default)
    {
        var notification = new Notification
        {
            Level   = request.Level,
            Message = request.Message,
            Source  = request.Source
        };

        if (request.Level < DispatchThreshold)
        {
            return new SendNotificationResponse(
                notification.Id,
                NotificationStatus.BelowThreshold,
                $"Notification received. Level '{request.Level}' is below the dispatch threshold – not forwarded.");
        }

        if (!_rateLimiter.TryAcquire())
        {
            return new SendNotificationResponse(
                notification.Id,
                NotificationStatus.RateLimitExceeded,
                "Rate limit of 10 messages per minute exceeded. Notification not forwarded.");
        }

        await _discordService.SendAsync(FormatMessage(notification), cancellationToken);

        return new SendNotificationResponse(
            notification.Id,
            NotificationStatus.Accepted,
            "Notification dispatched to Discord.");
    }

    private static string FormatMessage(Notification n)
    {
        var source = n.Source is not null ? $"\n*Source: {n.Source}*" : string.Empty;
        return $"**[{n.Level.ToString().ToUpperInvariant()}]** {n.Message}{source}";
    }
}
