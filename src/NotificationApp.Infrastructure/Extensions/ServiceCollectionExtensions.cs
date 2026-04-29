using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using NotificationApp.Domain.Interfaces;
using NotificationApp.Infrastructure.Discord;
using NotificationApp.Infrastructure.RateLimiting;

namespace NotificationApp.Infrastructure.Extensions;

public static class ServiceCollectionExtensions
{
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        services.Configure<DiscordOptions>(configuration.GetSection(DiscordOptions.SectionName));
        services.AddSingleton<IRateLimiter, SlidingWindowRateLimiter>();
        services.AddHttpClient<IDiscordService, DiscordWebhookService>();

        return services;
    }
}
