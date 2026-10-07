using System.ComponentModel.DataAnnotations;

namespace CarGarage.Api.Contracts;

public sealed record LoginRequest
{
    [Required, EmailAddress] public required string Email { get; init; }
    [Required] public required string Password { get; init; }
}

public sealed record RefreshRequest
{
    [Required, MinLength(10)] public required string RefreshToken { get; init; }
}
