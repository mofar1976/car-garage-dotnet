using CarGarage.Application.Abstractions;
using CarGarage.Application.Exceptions;
using CarGarage.Domain.Entities;
using CarGarage.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Npgsql;

namespace CarGarage.Infrastructure.Persistence.Repositories;

public sealed class AuthStore(CarGarageDbContext db) : IAuthStore
{
    public Task<User?> FindUserAsync(string email, AuthAudience audience, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Email == email && user.Audience == audience && !user.Deleted, cancellationToken);

    public Task<User?> FindUserByIdAsync(Guid id, CancellationToken cancellationToken) =>
        db.Users.AsNoTracking().SingleOrDefaultAsync(user => user.Id == id && !user.Deleted, cancellationToken);

    public async Task CreateUserAsync(User user, RefreshToken? token, CancellationToken cancellationToken)
    {
        db.Users.Add(user);
        if (token != null) db.RefreshTokens.Add(token);
        try { await db.SaveChangesAsync(cancellationToken); }
        catch (DbUpdateException error) when (error.InnerException is PostgresException
            { SqlState: PostgresErrorCodes.UniqueViolation, ConstraintName: "ux_users_email_audience_active" })
        {
            throw new AppException(AppError.Conflict, "An account with this email already exists");
        }
    }

    public async Task AddRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken)
    {
        db.RefreshTokens.Add(token);
        await db.SaveChangesAsync(cancellationToken);
    }

    public async Task<User?> RotateRefreshTokenAsync(string oldHash, AuthAudience audience, string newHash,
        DateTimeOffset now, DateTimeOffset expiresAt, CancellationToken cancellationToken)
    {
        await using var transaction = await db.Database.BeginTransactionAsync(cancellationToken);
        var stored = await db.RefreshTokens.AsNoTracking().SingleOrDefaultAsync(
            token => token.TokenHash == oldHash && token.Audience == audience, cancellationToken);
        if (stored == null) return null;

        // DELETE arbitrates concurrent refresh requests: only one can consume the token.
        var consumed = await db.RefreshTokens.Where(token => token.Id == stored.Id)
            .ExecuteDeleteAsync(cancellationToken);
        if (consumed != 1) return null;
        var user = await FindUserByIdAsync(stored.UserId, cancellationToken);
        if (stored.ExpiresAt <= now || user is not { IsActive: true } || user.Audience != audience)
        {
            await transaction.CommitAsync(cancellationToken);
            return null;
        }
        db.RefreshTokens.Add(new RefreshToken
        {
            UserId = user.Id, Audience = audience, TokenHash = newHash, CreatedAt = now, ExpiresAt = expiresAt
        });
        await db.SaveChangesAsync(cancellationToken);
        await transaction.CommitAsync(cancellationToken);
        return user;
    }

    public async Task DeleteRefreshTokenAsync(string hash, CancellationToken cancellationToken) =>
        await db.RefreshTokens.Where(token => token.TokenHash == hash).ExecuteDeleteAsync(cancellationToken);

    public async Task DeleteExpiredRefreshTokensAsync(DateTimeOffset now, CancellationToken cancellationToken) =>
        await db.RefreshTokens.Where(token => token.ExpiresAt <= now).ExecuteDeleteAsync(cancellationToken);
}
