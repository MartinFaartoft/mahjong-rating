using System.Net.Http.Headers;
using System.Security.Claims;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Rating.Infrastructure.Auth;

namespace Rating.Infrastructure.Tests;

public class BasicAuthenticationHandlerTests
{
    private static readonly AdminOptions Configured = new() { Username = "admin", Password = "s3cret" };

    private static async Task<(AuthenticateResult Result, HttpContext Context)> AuthenticateAsync(
        string? authorizationHeader,
        AdminOptions? options = null,
        Guid? seededAdminId = null)
    {
        var context = new DefaultHttpContext();
        if (authorizationHeader is not null)
        {
            context.Request.Headers.Authorization = authorizationHeader;
        }

        var accessor = new AdminUserAccessor();
        accessor.Set(seededAdminId ?? Guid.NewGuid());

        var schemeOptions = new TestOptionsMonitor<AuthenticationSchemeOptions>(new AuthenticationSchemeOptions());
        var adminOpts = new TestOptionsMonitor<AdminOptions>(options ?? Configured);
        var handler = new BasicAuthenticationHandler(schemeOptions, NullLoggerFactory.Instance, UrlEncoder.Default, adminOpts, accessor);
        await handler.InitializeAsync(
            new AuthenticationScheme(BasicAuthenticationDefaults.Scheme, null, typeof(BasicAuthenticationHandler)),
            context);
        var result = await handler.AuthenticateAsync();
        return (result, context);
    }

    private static string Header(string user, string pass)
        => "Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes($"{user}:{pass}"));

    [Fact]
    public async Task NoHeader_ReturnsNoResult()
    {
        var (result, _) = await AuthenticateAsync(null);
        Assert.True(result.None);
    }

    [Fact]
    public async Task NonBasicScheme_ReturnsNoResult()
    {
        var (result, _) = await AuthenticateAsync("Bearer token");
        Assert.True(result.None);
    }

    [Fact]
    public async Task ValidCredentials_ReturnsSuccessWithAdminClaims()
    {
        var adminId = Guid.NewGuid();
        var (result, _) = await AuthenticateAsync(Header("admin", "s3cret"), seededAdminId: adminId);
        Assert.True(result.Succeeded);
        var principal = result.Principal!;
        Assert.Equal(adminId.ToString(), principal.FindFirstValue(ClaimTypes.NameIdentifier));
        Assert.Equal("admin", principal.FindFirstValue(ClaimTypes.Name));
        Assert.True(principal.IsInRole(BasicAuthenticationDefaults.AdminRole));
    }

    [Fact]
    public async Task WrongPassword_ReturnsFailure()
    {
        var (result, _) = await AuthenticateAsync(Header("admin", "wrong"));
        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failure);
    }

    [Fact]
    public async Task WrongUsername_ReturnsFailure()
    {
        var (result, _) = await AuthenticateAsync(Header("root", "s3cret"));
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task MalformedBase64_ReturnsFailure()
    {
        var (result, _) = await AuthenticateAsync("Basic %%%not-base64%%%");
        Assert.False(result.Succeeded);
        Assert.NotNull(result.Failure);
    }

    [Fact]
    public async Task NoColonInCredential_ReturnsFailure()
    {
        var (result, _) = await AuthenticateAsync("Basic " + Convert.ToBase64String(Encoding.UTF8.GetBytes("noColonHere")));
        Assert.False(result.Succeeded);
    }

    [Fact]
    public async Task AdminNotConfigured_ReturnsFailure()
    {
        var (result, _) = await AuthenticateAsync(Header("admin", "s3cret"),
            options: new AdminOptions { Username = "", Password = "" });
        Assert.False(result.Succeeded);
    }

    private sealed class TestOptionsMonitor<T>(T value) : IOptionsMonitor<T>
    {
        public T CurrentValue { get; } = value;
        public T Get(string? name) => CurrentValue;
        public IDisposable? OnChange(Action<T, string?> listener) => null;
    }
}
