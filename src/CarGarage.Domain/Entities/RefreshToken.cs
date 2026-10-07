using CarGarage.Domain.Enums;

namespace CarGarage.Domain.Entities;

public sealed class RefreshToken
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public Guid UserId { get; set; }
    public required string TokenHash { get; set; }
    public AuthAudience Audience { get; set; }
    public DateTimeOffset ExpiresAt { get; set; }
    public string? UserAgent { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
}
