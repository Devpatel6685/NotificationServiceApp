namespace NotificationApp.Infrastructure.Discord;

public sealed class DiscordOptions
{
    public const string SectionName = "Discord";

    public string WebhookUrl { get; set; } = string.Empty;
}
