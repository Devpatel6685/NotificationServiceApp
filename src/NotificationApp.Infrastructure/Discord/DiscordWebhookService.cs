using System.Net.Http.Json;
using Microsoft.Extensions.Options;
using NotificationApp.Domain.Interfaces;

namespace NotificationApp.Infrastructure.Discord;

public sealed class DiscordWebhookService : IDiscordService
{
    private readonly HttpClient _httpClient;
    private readonly DiscordOptions _options;

    public DiscordWebhookService(HttpClient httpClient, IOptions<DiscordOptions> options)
    {
        _httpClient = httpClient;
        _options    = options.Value;
    }

    public async Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(_options.WebhookUrl))
            return;

        var payload  = new DiscordPayload(message);
        var response = await _httpClient.PostAsJsonAsync(_options.WebhookUrl, payload, cancellationToken);
        response.EnsureSuccessStatusCode();
    }
}
