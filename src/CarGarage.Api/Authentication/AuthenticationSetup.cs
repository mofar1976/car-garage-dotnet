using System.Security.Claims;
using System.Text;
using CarGarage.Application.Abstractions;
using CarGarage.Domain.Enums;
using CarGarage.Infrastructure.Configuration;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;

namespace CarGarage.Api.Authentication;

public static class AuthenticationSetup
{
    public static IServiceCollection AddPortalAuthentication(this IServiceCollection services)
    {
        services.AddOptions<JwtOptions>().ValidateOnStart();
        services.AddOptions<PasswordOptions>().ValidateOnStart();
        var authentication = services.AddAuthentication();
        foreach (var audience in Enum.GetValues<AuthAudience>())
        {
            var scheme = audience.ToString();
            authentication.AddJwtBearer(scheme, _ => { });
            services.AddOptions<JwtBearerOptions>(scheme).Configure<IOptions<JwtOptions>>((options, jwt) =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true, ValidIssuer = jwt.Value.Issuer,
                    ValidateAudience = true, ValidAudience = scheme,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(jwt.Value.Secret)),
                    ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
                    ValidateLifetime = true, RequireExpirationTime = true, RequireSignedTokens = true,
                    ClockSkew = TimeSpan.Zero, NameClaimType = "sub", RoleClaimType = "roles"
                };
                options.Events = new JwtBearerEvents
                {
                    OnTokenValidated = async context =>
                    {
                        if (!Guid.TryParse(context.Principal?.FindFirst("sub")?.Value, out var id))
                        {
                            context.Fail("Invalid token");
                            return;
                        }
                        var store = context.HttpContext.RequestServices.GetRequiredService<IAuthStore>();
                        var user = await store.FindUserByIdAsync(id, context.HttpContext.RequestAborted);
                        if (user is not { IsActive: true } || user.Audience != audience)
                        {
                            context.Fail("Invalid token");
                            return;
                        }
                        // Current database roles apply immediately, including revoked roles.
                        var identity = (ClaimsIdentity)context.Principal!.Identity!;
                        foreach (var role in identity.FindAll("roles").ToArray()) identity.RemoveClaim(role);
                        identity.AddClaims(user.Roles.Select(role => new Claim("roles", role.ToString())));
                    },
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        context.Response.StatusCode = 401;
                        context.Response.Headers.WWWAuthenticate = "Bearer";
                        var message = context.AuthenticateFailure is SecurityTokenExpiredException
                            ? "Token expired"
                            : string.IsNullOrEmpty(context.Request.Headers.Authorization) ? "Missing bearer token" : "Invalid token";
                        await context.Response.WriteAsJsonAsync(new { error = message });
                    },
                    OnForbidden = async context =>
                    {
                        context.Response.StatusCode = 403;
                        await context.Response.WriteAsJsonAsync(new { error = "Insufficient permissions" });
                    }
                };
            });
        }
        services.AddAuthorization(options =>
        {
            foreach (var audience in Enum.GetValues<AuthAudience>())
                options.AddPolicy(audience.ToString(), policy => policy
                    .AddAuthenticationSchemes(audience.ToString()).RequireAuthenticatedUser());
            options.AddPolicy("PlatformAdmin", policy => policy.AddAuthenticationSchemes("ADMIN")
                .RequireAuthenticatedUser().RequireRole("PLATFORM_ADMIN"));
        });
        services.AddHostedService<RefreshTokenCleanupService>();
        return services;
    }
}
