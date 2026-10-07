using System.ComponentModel.DataAnnotations;
using CarGarage.Application.Abstractions;
using CarGarage.Application.Exceptions;
using CarGarage.Application.Models;
using CarGarage.Domain.Entities;
using CarGarage.Domain.Enums;

namespace CarGarage.Application.Services;

public sealed class UserRegistrationService(IPasswordHasher passwords, IAuthStore store)
{
    public User BuildUser(AuthAudience audience, RegisterInput input)
    {
        var errors = new List<ValidationResult>();
        if (!Validator.TryValidateObject(input, new ValidationContext(input), errors, true))
            throw new AppException(AppError.Validation, "ValidationError", new
            {
                formErrors = Array.Empty<string>(),
                fieldErrors = errors.SelectMany(error => error.MemberNames.Select(member => (member, error.ErrorMessage)))
                    .GroupBy(error => char.ToLowerInvariant(error.member[0]) + error.member[1..])
                    .ToDictionary(group => group.Key, group => group.Select(error => error.ErrorMessage).ToArray())
            });

        return new User
        {
            Email = input.Email.Trim().ToLowerInvariant(),
            PasswordHash = passwords.Hash(input.Password),
            FirstName = input.FirstName.Trim(),
            LastName = input.LastName.Trim(),
            Phone = input.Phone?.Trim(),
            Audience = audience,
            Roles = audience switch
            {
                AuthAudience.ADMIN => [UserRole.PLATFORM_ADMIN],
                AuthAudience.WORKSHOP => [UserRole.WORKSHOP_MEMBER],
                AuthAudience.CUSTOMER => [UserRole.CUSTOMER],
                _ => throw new ArgumentOutOfRangeException(nameof(audience))
            }
        };
    }

    public async Task<AuthUser> CreateAdminAsync(RegisterInput input, CancellationToken cancellationToken = default)
    {
        var user = BuildUser(AuthAudience.ADMIN, input);
        await store.CreateUserAsync(user, null, cancellationToken);
        return AuthService.ToAuthUser(user);
    }
}
