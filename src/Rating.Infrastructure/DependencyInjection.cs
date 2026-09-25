using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Rating.Application.Abstractions;
using Rating.Infrastructure.Auth;
using Rating.Infrastructure.Persistence;
using Rating.Infrastructure.Persistence.Repositories;

namespace Rating.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services, IConfiguration configuration)
    {
        var cs = configuration.GetConnectionString("Default")
            ?? throw new InvalidOperationException("ConnectionStrings:Default is not configured.");

        services.AddDbContext<AppDbContext>(options => options.UseNpgsql(cs, npg => npg
            .MigrationsAssembly(typeof(AppDbContext).Assembly.FullName)));

        services.AddScoped<IUnitOfWork, UnitOfWork>();
        services.AddScoped<IPlayerRepository, PlayerRepository>();
        services.AddScoped<IGameRepository, GameRepository>();
        services.AddScoped<IRatingHistoryRepository, RatingHistoryRepository>();
        services.AddSingleton<IClock, Time.SystemClock>();

        services.Configure<AdminOptions>(configuration.GetSection(AdminOptions.SectionName));
        services.AddSingleton<AdminUserAccessor>();
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, HttpContextCurrentUser>();

        services.AddAuthentication(BasicAuthenticationDefaults.Scheme)
            .AddScheme<AuthenticationSchemeOptions, BasicAuthenticationHandler>(
                BasicAuthenticationDefaults.Scheme, _ => { });
        services.AddAuthorization(o => o.AddPolicy(
            BasicAuthenticationDefaults.AdminPolicy,
            p => p.RequireAuthenticatedUser().RequireRole(BasicAuthenticationDefaults.AdminRole)));

        return services;
    }
}
