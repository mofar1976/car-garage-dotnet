using CarGarage.Application.Abstractions;
using CarGarage.Application.Services;
using CarGarage.Infrastructure.Authentication;
using CarGarage.Infrastructure.Persistence.Repositories;
using CarGarage.Infrastructure.Configuration;
using CarGarage.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Options;

namespace CarGarage.Infrastructure;

public static class DependencyInjection
{
    public static IServiceCollection AddInfrastructure(this IServiceCollection services)
    {
        services.AddOptions<DatabaseOptions>()
            .BindConfiguration(DatabaseOptions.SectionName)
            .Validate(options => options.IsValid(),
                "Database:ConnectionString must be a valid PostgreSQL connection string with Host, Database and Username.")
            .ValidateOnStart();
        services.AddOptions<JwtOptions>().BindConfiguration(JwtOptions.SectionName)
            .Validate(options => options.IsValid(), "Jwt requires a secret of at least 32 UTF-8 bytes, an issuer and valid token lifetimes.");
        services.AddOptions<PasswordOptions>().BindConfiguration(PasswordOptions.SectionName)
            .Validate(options => options.WorkFactor is >= 4 and <= 15, "Password:WorkFactor must be between 4 and 15.");
        services.AddScoped<IAuthStore, AuthStore>();
        services.AddScoped<IPasswordHasher, BcryptPasswordHasher>();
        services.AddScoped<ITokenIssuer, JwtTokenIssuer>();
        services.AddScoped<UserRegistrationService>();
        services.AddScoped<AuthService>();
        services.TryAddSingleton(TimeProvider.System);
        services.AddDbContext<CarGarageDbContext>((provider, options) =>
            options.UseNpgsql(provider.GetRequiredService<IOptions<DatabaseOptions>>().Value.ConnectionString));
        return services;
    }
}
