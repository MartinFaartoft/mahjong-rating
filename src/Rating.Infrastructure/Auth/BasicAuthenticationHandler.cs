using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Rating.Infrastructure.Auth;

/// <summary>
/// Minimal HTTP Basic authentication against the single admin credential
/// configured via <see cref="AdminOptions"/>. Credentials are compared in
/// constant time. Missing / malformed headers result in
/// <see cref="AuthenticateResult.NoResult"/> so anonymous endpoints remain
/// reachable; wrong credentials yield <see cref="AuthenticateResult.Fail"/>.
/// </summary>
public sealed class BasicAuthenticationHandler(
    IOptionsMonitor<AuthenticationSchemeOptions> options,
    ILoggerFactory logger,
    UrlEncoder encoder,
    IOptionsMonitor<AdminOptions> admin,
    AdminUserAccessor adminUser)
    : AuthenticationHandler<AuthenticationSchemeOptions>(options, logger, encoder)
{
    protected override Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue("Authorization", out var header) || header.Count == 0)
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        var value = header.ToString();
        const string prefix = "Basic ";
        if (!value.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
        {
            return Task.FromResult(AuthenticateResult.NoResult());
        }

        string username, password;
        try
        {
            var decoded = Convert.FromBase64String(value[prefix.Length..].Trim());
            var pair = Encoding.UTF8.GetString(decoded);
            var colon = pair.IndexOf(':');
            if (colon < 0)
            {
                return Task.FromResult(AuthenticateResult.Fail("Malformed Basic credential."));
            }
            username = pair[..colon];
            password = pair[(colon + 1)..];
        }
        catch (FormatException)
        {
            return Task.FromResult(AuthenticateResult.Fail("Malformed Basic credential."));
        }

        var configured = admin.CurrentValue;
        if (string.IsNullOrEmpty(configured.Username) || string.IsNullOrEmpty(configured.Password))
        {
            return Task.FromResult(AuthenticateResult.Fail("Admin credentials not configured."));
        }

        if (!FixedTimeEquals(username, configured.Username) || !FixedTimeEquals(password, configured.Password))
        {
            return Task.FromResult(AuthenticateResult.Fail("Invalid credentials."));
        }

        var identity = new ClaimsIdentity(new[]
        {
            new Claim(ClaimTypes.NameIdentifier, adminUser.AdminUserId.ToString()),
            new Claim(ClaimTypes.Name, configured.Username),
            new Claim(ClaimTypes.Role, BasicAuthenticationDefaults.AdminRole),
        }, Scheme.Name);

        var principal = new ClaimsPrincipal(identity);
        var ticket = new AuthenticationTicket(principal, Scheme.Name);
        return Task.FromResult(AuthenticateResult.Success(ticket));
    }

    protected override Task HandleChallengeAsync(AuthenticationProperties properties)
    {
        Response.Headers.WWWAuthenticate = $"Basic realm=\"rating\", charset=\"UTF-8\"";
        return base.HandleChallengeAsync(properties);
    }

    private static bool FixedTimeEquals(string a, string b)
    {
        var ab = Encoding.UTF8.GetBytes(a);
        var bb = Encoding.UTF8.GetBytes(b);
        if (ab.Length != bb.Length) return false;
        return CryptographicOperations.FixedTimeEquals(ab, bb);
    }
}
