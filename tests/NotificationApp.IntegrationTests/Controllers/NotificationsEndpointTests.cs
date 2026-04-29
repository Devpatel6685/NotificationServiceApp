using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using NotificationApp.Application.DTOs;
using NotificationApp.Domain.Enums;
using NotificationApp.Domain.Interfaces;
using NotificationApp.IntegrationTests.Helpers;

namespace NotificationApp.IntegrationTests.Controllers;

/// <summary>
/// End-to-end tests for POST /api/notifications.
/// The Discord service is replaced with a capturing test double so no real HTTP calls are made.
/// </summary>
public sealed class NotificationsEndpointTests : IDisposable
{
    private readonly CapturingDiscordService _discordCapture = new();
    private readonly WebApplicationFactory<Program> _factory;
    private readonly HttpClient _client;

    public NotificationsEndpointTests()
    {
        _factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Replace the real Discord service with a test double
                var discordDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IDiscordService));
                if (discordDescriptor is not null)
                    services.Remove(discordDescriptor);
                services.AddSingleton<IDiscordService>(_discordCapture);

                // Always-allow rate limiter so these tests are not affected by limits
                var rlDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IRateLimiter));
                if (rlDescriptor is not null)
                    services.Remove(rlDescriptor);
                services.AddSingleton<IRateLimiter>(new AlwaysAllowRateLimiter());
            });
        });

        _client = _factory.CreateClient();
    }

    // ── Happy-path tests ─────────────────────────────────────────────────────

    [Theory]
    [InlineData(NotificationLevel.Debug)]
    [InlineData(NotificationLevel.Info)]
    public async Task Post_LevelBelowWarning_Returns200_NotDispatched(NotificationLevel level)
    {
        var request = new SendNotificationRequest { Level = level, Message = "informational" };

        var response = await _client.PostAsJsonAsync("/api/notifications", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SendNotificationResponse>();
        body!.Dispatched.Should().BeFalse();
        body.Status.Should().Be(NotificationStatus.BelowThreshold);
        _discordCapture.SentMessages.Should().BeEmpty();
    }

    [Theory]
    [InlineData(NotificationLevel.Warning)]
    [InlineData(NotificationLevel.Error)]
    [InlineData(NotificationLevel.Critical)]
    public async Task Post_LevelAtOrAboveWarning_Returns200_Dispatched(NotificationLevel level)
    {
        var request = new SendNotificationRequest { Level = level, Message = "alert message" };

        var response = await _client.PostAsJsonAsync("/api/notifications", request);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var body = await response.Content.ReadFromJsonAsync<SendNotificationResponse>();
        body!.Dispatched.Should().BeTrue();
        body.Status.Should().Be(NotificationStatus.Accepted);
        _discordCapture.SentMessages.Should().ContainSingle();
    }

    [Fact]
    public async Task Post_DispatchedMessage_ContainsLevelAndMessageInDiscordPayload()
    {
        var request = new SendNotificationRequest
        {
            Level   = NotificationLevel.Error,
            Message = "Disk full on server01",
            Source  = "MonitoringAgent"
        };

        await _client.PostAsJsonAsync("/api/notifications", request);

        _discordCapture.SentMessages.Should().ContainSingle()
            .Which.Should()
            .Contain("ERROR", Exactly.Once())
            .And.Contain("Disk full on server01")
            .And.Contain("MonitoringAgent");
    }

    // ── Validation tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task Post_EmptyMessage_Returns400()
    {
        var request = new { Level = NotificationLevel.Warning, Message = "" };

        var response = await _client.PostAsJsonAsync("/api/notifications", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_MissingBody_Returns400()
    {
        var response = await _client.PostAsync(
            "/api/notifications",
            new StringContent(string.Empty, System.Text.Encoding.UTF8, "application/json"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    public void Dispose()
    {
        _client.Dispose();
        _factory.Dispose();
    }
}
