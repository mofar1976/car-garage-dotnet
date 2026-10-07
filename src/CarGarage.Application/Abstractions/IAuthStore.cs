using CarGarage.Domain.Entities;
using CarGarage.Domain.Enums;

namespace CarGarage.Application.Abstractions;

public interface IAuthStore
{
    Task<User?> FindUserAsync(string email, AuthAudience audience, CancellationToken cancellationToken);
    Task<User?> FindUserByIdAsync(Guid id, CancellationToken cancellationToken);
    Task CreateUserAsync(User user, RefreshToken? token, CancellationToken cancellationToken);
    Task AddRefreshTokenAsync(RefreshToken token, CancellationToken cancellationToken);
    Task<User?> RotateRefreshTokenAsync(string oldHash, AuthAudience audience, string newHash,
        DateTimeOffset now, DateTimeOffset expiresAt, CancellationToken cancellationToken);
    Task DeleteRefreshTokenAsync(string hash, CancellationToken cancellationToken);
    Task DeleteExpiredRefreshTokensAsync(DateTimeOffset now, CancellationToken cancellationToken);
}
