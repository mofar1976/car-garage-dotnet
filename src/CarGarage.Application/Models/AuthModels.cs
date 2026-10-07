using System.ComponentModel.DataAnnotations;
using System.Text;

namespace CarGarage.Application.Models;

public sealed record RegisterInput : IValidatableObject
{
    [Required, EmailAddress] public required string Email { get; init; }
    [Required, StringLength(128, MinimumLength = 8)] public required string Password { get; init; }
    [Required, StringLength(80)] public required string FirstName { get; init; }
    [Required, StringLength(80)] public required string LastName { get; init; }
    [StringLength(30, MinimumLength = 3)] public string? Phone { get; init; }

    public IEnumerable<ValidationResult> Validate(ValidationContext validationContext)
    {
        if (Password != null && Encoding.UTF8.GetByteCount(Password) > 72)
            yield return new ValidationResult("Password must be at most 72 UTF-8 bytes for bcrypt.", [nameof(Password)]);
    }
}

public sealed record AuthUser(Guid Id, string Email, string FirstName, string LastName,
    string? Phone, string Audience, string[] Roles, DateTimeOffset CreatedAt);
public sealed record AuthTokens(string AccessToken, string RefreshToken, int ExpiresIn);
public sealed record AuthResponse(AuthUser User, AuthTokens Tokens);
