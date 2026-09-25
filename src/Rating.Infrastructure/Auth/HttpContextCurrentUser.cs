using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Rating.Application.Abstractions;

namespace Rating.Infrastructure.Auth;

internal sealed class HttpContextCurrentUser(IHttpContextAccessor http) : ICurrentUser
{
    public Guid? UserId
    {
        get
        {
            var value = http.HttpContext?.User.FindFirstValue(ClaimTypes.NameIdentifier);
            return Guid.TryParse(value, out var id) ? id : null;
        }
    }

    public string? Username => http.HttpContext?.User.FindFirstValue(ClaimTypes.Name);

    public bool IsAdmin => http.HttpContext?.User.IsInRole(BasicAuthenticationDefaults.AdminRole) ?? false;
}
