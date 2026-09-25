using Microsoft.Extensions.DependencyInjection;
using Rating.Application.Services;

namespace Rating.Application;

public static class DependencyInjection
{
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        services.AddScoped<IPlayerService, PlayerService>();
        services.AddScoped<IGameService, GameService>();
        services.AddScoped<IRatingService, RatingService>();
        return services;
    }
}
