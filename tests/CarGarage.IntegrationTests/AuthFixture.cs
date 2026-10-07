using CarGarage.Application.Models;
using CarGarage.Application.Services;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace CarGarage.IntegrationTests;

public sealed class AuthFixture : IAsyncLifetime
{
    public const string Secret = "test-only-auth-secret-64-bytes-long-never-use-this-in-production-12345";
    public PostgresFixture Database { get; } = new();
    public WebApplicationFactory<Program> Factory { get; private set; } = null!;

    public async Task InitializeAsync()
    {
        await Database.InitializeAsync();
        Factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment("Development");
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = Database.ConnectionString,
                ["Jwt:Secret"] = Secret,
                ["Password:WorkFactor"] = "4"
            }));
        });
    }

    public async Task<AuthUser> CreateAdminAsync(string email, string password)
    {
        await using var scope = Factory.Services.CreateAsyncScope();
        return await scope.ServiceProvider.GetRequiredService<UserRegistrationService>().CreateAdminAsync(new RegisterInput
        {
            Email = email, Password = password, FirstName = "Platform", LastName = "Admin"
        });
    }

    public async Task DisposeAsync()
    {
        await Factory.DisposeAsync();
        await Database.DisposeAsync();
    }
}
