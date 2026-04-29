using Microsoft.AspNetCore.Mvc;
using NotificationApp.Application.DTOs;
using NotificationApp.Application.Interfaces;

namespace NotificationApp.Controllers;

[ApiController]
[Route("api/[controller]")]
[Produces("application/json")]
public sealed class NotificationsController : ControllerBase
{
    private readonly INotificationService _notificationService;

    public NotificationsController(INotificationService notificationService)
        => _notificationService = notificationService;

    [HttpPost]
    [ProducesResponseType(typeof(SendNotificationResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(typeof(SendNotificationResponse), StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> Send(
        [FromBody] SendNotificationRequest request,
        CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return BadRequest(ModelState);

        var result = await _notificationService.ProcessAsync(request, cancellationToken);

        return result.Status == NotificationStatus.RateLimitExceeded
            ? StatusCode(StatusCodes.Status429TooManyRequests, result)
            : Ok(result);
    }
}
