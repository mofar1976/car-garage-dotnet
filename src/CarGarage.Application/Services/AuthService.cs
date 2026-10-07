using System.Security.Cryptography;
using System.Text;
using CarGarage.Application.Abstractions;
using CarGarage.Application.Exceptions;
using CarGarage.Application.Models;
using CarGarage.Domain.Entities;
using CarGarage.Domain.Enums;

namespace CarGarage.Application.Services;

public sealed class AuthService(IAuthStore store, IPasswordHasher passwords, ITokenIssuer issuer,
    UserRegistrationService registration, TimeProvider clock)
{
    public async Task<AuthResponse> RegisterAsync(AuthAudience audience, RegisterInput input, CancellationToken cancellationToken)
    {
        var user = registration.BuildUser(audience, input);
        var (tokens, token) = IssueTokens(user, null);
        await store.CreateUserAsync(user, token, cancellationToken);
        return new(ToAuthUser(user), tokens);
    }

    public async Task<AuthResponse> LoginAsync(AuthAudience audience, string email, string password,
        string? userAgent, CancellationToken cancellationToken)
    {
        var user = await store.FindUserAsync(email.Trim().ToLowerInvariant(), audience, cancellationToken);
        if (user is not { IsActive: true } || !passwords.Verify(password, user.PasswordHash))
            throw new AppException(AppError.Unauthorized, "Invalid credentials");
        var (tokens, token) = IssueTokens(user, userAgent);
        await store.AddRefreshTokenAsync(token, cancellationToken);
        return new(ToAuthUser(user), tokens);
    }

    public async Task<AuthResponse> RefreshAsync(AuthAudience audience, string oldToken, CancellationToken cancellationToken)
    {
        var raw = NewRefreshToken();
        var now = clock.GetUtcNow();
        var user = await store.RotateRefreshTokenAsync(HashToken(oldToken), audience, HashToken(raw),
            now, now.Add(issuer.RefreshLifetime), cancellationToken);
        if (user == null)
            throw new AppException(AppError.Unauthorized, "Invalid refresh token");
        return new(ToAuthUser(user), new(issuer.CreateAccessToken(user), raw, issuer.AccessLifetimeSeconds));
    }

    public Task LogoutAsync(string token, CancellationToken cancellationToken) =>
        store.DeleteRefreshTokenAsync(HashToken(token), cancellationToken);

    public async Task<AuthUser> GetUserAsync(Guid id, CancellationToken cancellationToken)
    {
        var user = await store.FindUserByIdAsync(id, cancellationToken);
        if (user is not { IsActive: true }) throw new AppException(AppError.Unauthorized, "Unauthorized");
        return ToAuthUser(user);
    }

    public static AuthUser ToAuthUser(User user) => new(user.Id, user.Email, user.FirstName, user.LastName,
        user.Phone, user.Audience.ToString(), user.Roles.Select(role => role.ToString()).ToArray(), user.CreatedAt);

    public static string HashToken(string token) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    private static string NewRefreshToken() => Convert.ToBase64String(RandomNumberGenerator.GetBytes(32))
        .TrimEnd('=').Replace('+', '-').Replace('/', '_');

    private (AuthTokens Tokens, RefreshToken Stored) IssueTokens(User user, string? userAgent)
    {
        var raw = NewRefreshToken();
        var now = clock.GetUtcNow();
        return (new(issuer.CreateAccessToken(user), raw, issuer.AccessLifetimeSeconds), new RefreshToken
        {
            UserId = user.Id, Audience = user.Audience, TokenHash = HashToken(raw),
            ExpiresAt = now.Add(issuer.RefreshLifetime), CreatedAt = now, UserAgent = userAgent
        });
    }
}
