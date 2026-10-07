using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Xunit;

namespace CarGarage.IntegrationTests;

public sealed class JwtConfigurationTests
{
    [Theory]
    [InlineData("Jwt:Secret", "")]
    [InlineData("Jwt:Secret", "short-secret")]
    [InlineData("Jwt:AccessTtlSeconds", "0")]
    [InlineData("Jwt:RefreshTtlDays", "0")]
    [InlineData("Password:WorkFactor", "3")]
    public void InvalidAuthenticationConfigurationFailsAtStartup(string key, string value)
    {
        var settings = new Dictionary<string, string?>
            {
                ["Database:ConnectionString"] = "Host=localhost;Database=unused;Username=unused",
                ["Jwt:Secret"] = AuthFixture.Secret
            };
        settings[key] = value;
        using var factory = new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(settings)));
        var error = Assert.Throws<OptionsValidationException>(() => factory.CreateClient());
        Assert.DoesNotContain("short-secret", error.Message);
    }
}
