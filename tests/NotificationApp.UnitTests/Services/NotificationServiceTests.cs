using FluentAssertions;
using Moq;
using NotificationApp.Application.DTOs;
using Xunit;
using NotificationApp.Application.Services;
using NotificationApp.Domain.Enums;
using NotificationApp.Domain.Interfaces;

namespace NotificationApp.UnitTests.Services;

public sealed class NotificationServiceTests
{
    private readonly Mock<IDiscordService> _discordMock  = new();
    private readonly Mock<IRateLimiter>   _rateLimiterMock = new();
    private readonly NotificationService  _sut;

    public NotificationServiceTests()
        => _sut = new NotificationService(_discordMock.Object, _rateLimiterMock.Object);

    // ── Below-threshold tests ────────────────────────────────────────────────

    [Theory]
    [InlineData(NotificationLevel.Debug)]
    [InlineData(NotificationLevel.Info)]
    public async Task ProcessAsync_LevelBelowWarning_DoesNotDispatch(NotificationLevel level)
    {
        var request = new SendNotificationRequest { Level = level, Message = "test" };

        var result = await _sut.ProcessAsync(request);

        result.Dispatched.Should().BeFalse();
        result.Status.Should().Be(NotificationStatus.BelowThreshold);
        _discordMock.Verify(
            x => x.SendAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Theory]
    [InlineData(NotificationLevel.Debug)]
    [InlineData(NotificationLevel.Info)]
    public async Task ProcessAsync_LevelBelowWarning_NeverCallsRateLimiter(NotificationLevel level)
    {
        var request = new SendNotificationRequest { Level = level, Message = "test" };

        await _sut.ProcessAsync(request);

        // Short-circuit must happen before the rate limiter is consulted.
        _rateLimiterMock.Verify(x => x.TryAcquire(), Times.Never);
    }

    // ── At-or-above-threshold tests ──────────────────────────────────────────

    [Theory]
    [InlineData(NotificationLevel.Warning)]
    [InlineData(NotificationLevel.Error)]
    [InlineData(NotificationLevel.Critical)]
    public async Task ProcessAsync_LevelAtOrAboveWarning_DispatchesToDiscord(NotificationLevel level)
    {
        _rateLimiterMock.Setup(x => x.TryAcquire()).Returns(true);
        var request = new SendNotificationRequest { Level = level, Message = "alert!" };

        var result = await _sut.ProcessAsync(request);

        result.Dispatched.Should().BeTrue();
        result.Status.Should().Be(NotificationStatus.Accepted);
        _discordMock.Verify(
            x => x.SendAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Once);
    }

    [Fact]
    public async Task ProcessAsync_DispatchedMessage_ContainsLevelAndMessageText()
    {
        _rateLimiterMock.Setup(x => x.TryAcquire()).Returns(true);
        var request = new SendNotificationRequest
        {
            Level   = NotificationLevel.Error,
            Message = "Database connection lost",
            Source  = "OrderService"
        };

        await _sut.ProcessAsync(request);

        _discordMock.Verify(
            x => x.SendAsync(
                It.Is<string>(msg =>
                    msg.Contains("ERROR", StringComparison.OrdinalIgnoreCase) &&
                    msg.Contains("Database connection lost") &&
                    msg.Contains("OrderService")),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }

    // ── Rate-limit tests ──────────────────────────────────────────────────────

    [Fact]
    public async Task ProcessAsync_RateLimitExceeded_DoesNotDispatch()
    {
        _rateLimiterMock.Setup(x => x.TryAcquire()).Returns(false);
        var request = new SendNotificationRequest { Level = NotificationLevel.Warning, Message = "flood" };

        var result = await _sut.ProcessAsync(request);

        result.Dispatched.Should().BeFalse();
        result.Status.Should().Be(NotificationStatus.RateLimitExceeded);
        _discordMock.Verify(
            x => x.SendAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()),
            Times.Never);
    }

    [Fact]
    public async Task ProcessAsync_ReturnsUniqueNotificationId_EachCall()
    {
        _rateLimiterMock.Setup(x => x.TryAcquire()).Returns(true);
        var request = new SendNotificationRequest { Level = NotificationLevel.Warning, Message = "x" };

        var r1 = await _sut.ProcessAsync(request);
        var r2 = await _sut.ProcessAsync(request);

        r1.NotificationId.Should().NotBe(r2.NotificationId);
    }
}
