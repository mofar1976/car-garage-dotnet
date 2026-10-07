using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace CarGarage.IntegrationTests;

public sealed class DatabaseConfigurationTests
{
    [Theory]
    [InlineData("")]
    [InlineData("not-a-connection-string")]
    [InlineData("Host=localhost;Username=test")]
    [InlineData("Database=test;Username=test")]
    [InlineData("Host=localhost;Database=test")]
    public void InvalidConfigurationFailsAtStartupWithoutExposingConnectionString(string connectionString)
    {
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-only-secret-for-http-tests-32-bytes-minimum",
                ["Password:WorkFactor"] = "4",
                ["Database:ConnectionString"] = connectionString
            })));
        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.Contains("Database:ConnectionString", error.Message);
        Assert.DoesNotContain("not-a-connection-string", error.Message);
    }
}
