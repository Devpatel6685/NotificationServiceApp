using System.Text.Json.Serialization;

namespace NotificationApp.Infrastructure.Discord;

/// <summary>
/// JSON payload accepted by a Discord incoming webhook.
/// </summary>
internal sealed record DiscordPayload(
    [property: JsonPropertyName("content")] string Content);
