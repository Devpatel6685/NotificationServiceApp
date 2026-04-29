using NotificationApp.Domain.Interfaces;

namespace NotificationApp.IntegrationTests.Helpers;

/// <summary>
/// Test double that records all messages sent to it (no real HTTP calls).
/// </summary>
internal sealed class CapturingDiscordService : IDiscordService
{
    private readonly List<string> _messages = new();

    public IReadOnlyList<string> SentMessages => _messages.AsReadOnly();

    public Task SendAsync(string message, CancellationToken cancellationToken = default)
    {
        _messages.Add(message);
        return Task.CompletedTask;
    }
}
