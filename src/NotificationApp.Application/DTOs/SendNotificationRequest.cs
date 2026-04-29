using System.ComponentModel.DataAnnotations;
using NotificationApp.Domain.Enums;

namespace NotificationApp.Application.DTOs;

public sealed class SendNotificationRequest
{
    [Required]
    public NotificationLevel Level { get; init; }

    [Required]
    [StringLength(2000, MinimumLength = 1)]
    public string Message { get; init; } = string.Empty;

    [StringLength(200)]
    public string? Source { get; init; }
}
