using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;
using Microsoft.Extensions.DependencyInjection;
using NotificationApp.Application.DTOs;
using NotificationApp.Domain.Enums;
using NotificationApp.Domain.Interfaces;
using NotificationApp.Infrastructure.RateLimiting;
using NotificationApp.IntegrationTests.Helpers;

namespace NotificationApp.IntegrationTests.Controllers;

/// <summary>
/// Integration tests that verify the 10-messages-per-minute rate limit is enforced end-to-end.
/// Each test creates its own factory with a fresh SlidingWindowRateLimiter for full isolation.
/// </summary>
public sealed class RateLimitIntegrationTests
{
    private static HttpClient BuildClient(IRateLimiter rateLimiter)
    {
        var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.ConfigureServices(services =>
            {
                // Capture Discord so no real HTTP calls are made
                var discordDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IDiscordService));
                if (discordDescriptor is not null) services.Remove(discordDescriptor);
                services.AddSingleton<IDiscordService>(new CapturingDiscordService());

                // Inject the caller-supplied rate limiter
                var rlDescriptor = services.SingleOrDefault(
                    d => d.ServiceType == typeof(IRateLimiter));
                if (rlDescriptor is not null) services.Remove(rlDescriptor);
                services.AddSingleton(rateLimiter);
            });
        });

        return factory.CreateClient();
    }

    [Fact]
    public async Task First10WarningRequests_AllReturn200_Accepted()
    {
        using var client  = BuildClient(new SlidingWindowRateLimiter());
        var request = new SendNotificationRequest { Level = NotificationLevel.Warning, Message = "test" };

        for (var i = 0; i < 10; i++)
        {
            var response = await client.PostAsJsonAsync("/api/notifications", request);
            response.StatusCode.Should().Be(HttpStatusCode.OK, $"request #{i + 1} should be within limit");

            var body = await response.Content.ReadFromJsonAsync<SendNotificationResponse>();
            body!.Dispatched.Should().BeTrue($"request #{i + 1} should be dispatched");
        }
    }

    [Fact]
    public async Task EleventhWarningRequest_Returns429_NotDispatched()
    {
        using var client  = BuildClient(new SlidingWindowRateLimiter());
        var request = new SendNotificationRequest { Level = NotificationLevel.Warning, Message = "flood" };

        // Exhaust the limit
        for (var i = 0; i < 10; i++)
            await client.PostAsJsonAsync("/api/notifications", request);

        // 11th request must be rate-limited
        var response = await client.PostAsJsonAsync("/api/notifications", request);
        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);

        var body = await response.Content.ReadFromJsonAsync<SendNotificationResponse>();
        body!.Dispatched.Should().BeFalse();
        body.Status.Should().Be(NotificationStatus.RateLimitExceeded);
    }

    [Fact]
    public async Task ExhaustedRateLimiter_Returns429_ForEachRequest()
    {
        using var client  = BuildClient(new ExhaustedRateLimiter());
        var request = new SendNotificationRequest { Level = NotificationLevel.Critical, Message = "critical" };

        var response = await client.PostAsJsonAsync("/api/notifications", request);

        response.StatusCode.Should().Be(HttpStatusCode.TooManyRequests);
    }
}
