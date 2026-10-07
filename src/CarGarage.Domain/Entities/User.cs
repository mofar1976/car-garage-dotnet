using CarGarage.Domain.Enums;

namespace CarGarage.Domain.Entities;

public sealed class User
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public required string Email { get; set; }
    public required string PasswordHash { get; set; }
    public required string FirstName { get; set; }
    public required string LastName { get; set; }
    public string? Phone { get; set; }
    public required AuthAudience Audience { get; set; }
    public UserRole[] Roles { get; set; } = [];
    public bool IsActive { get; set; } = true;
    public bool Deleted { get; set; }
    public DateTimeOffset CreatedAt { get; set; }
    public DateTimeOffset UpdatedAt { get; set; }
}
