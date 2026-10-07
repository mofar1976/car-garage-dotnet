using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;
using CarGarage.Application.Abstractions;
using CarGarage.Domain.Entities;
using CarGarage.Infrastructure.Configuration;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CarGarage.Infrastructure.Authentication;

public sealed class JwtTokenIssuer(IOptions<JwtOptions> settings, TimeProvider clock) : ITokenIssuer
{
    public int AccessLifetimeSeconds => settings.Value.AccessTtlSeconds;
    public TimeSpan RefreshLifetime => TimeSpan.FromDays(settings.Value.RefreshTtlDays);

    public string CreateAccessToken(User user)
    {
        var options = settings.Value;
        var now = clock.GetUtcNow();
        var payload = new JwtPayload(options.Issuer, user.Audience.ToString(),
            [new Claim("sub", user.Id.ToString())], now.UtcDateTime,
            now.AddSeconds(options.AccessTtlSeconds).UtcDateTime, now.UtcDateTime)
        {
            ["roles"] = user.Roles.Select(role => role.ToString()).ToArray()
        };
        var header = new JwtHeader(new SigningCredentials(
            new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.Secret)), SecurityAlgorithms.HmacSha256));
        return new JwtSecurityTokenHandler().WriteToken(new JwtSecurityToken(header, payload));
    }
}
