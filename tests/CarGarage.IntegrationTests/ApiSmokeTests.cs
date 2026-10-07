using System.Net;
using System.Text.Json;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Xunit;

namespace CarGarage.IntegrationTests;

public sealed class ApiSmokeTests
{
    private static WebApplicationFactory<Program> CreateFactory(string environment = "Development") =>
        new WebApplicationFactory<Program>().WithWebHostBuilder(builder =>
        {
            builder.UseEnvironment(environment);
            builder.ConfigureAppConfiguration((_, config) => config.AddInMemoryCollection(new Dictionary<string, string?>
            {
                ["Jwt:Secret"] = "test-only-secret-for-http-tests-32-bytes-minimum",
                ["Password:WorkFactor"] = "4",
                ["Database:ConnectionString"] = "Host=localhost;Database=unused_smoke_test;Username=unused"
            }));
        });

    [Fact]
    public async Task HealthMatchesReferenceContract()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var before = DateTimeOffset.UtcNow;
        using var response = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var body = json.RootElement;
        Assert.Equal(3, body.EnumerateObject().Count());
        Assert.Equal("ok", body.GetProperty("status").GetString());
        Assert.Equal("car-garage-api", body.GetProperty("service").GetString());
        var timestamp = body.GetProperty("timestamp").GetDateTimeOffset();
        Assert.Equal(TimeSpan.Zero, timestamp.Offset);
        Assert.InRange(timestamp, before, DateTimeOffset.UtcNow);
    }

    [Fact]
    public async Task SwaggerExposesHealthEndpointAndInteractivePage()
    {
        await using var factory = CreateFactory();
        using var client = factory.CreateClient();
        var html = await client.GetStringAsync("/swagger/index.html");
        Assert.Contains("Swagger UI", html);
        using var json = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));
        Assert.True(json.RootElement.GetProperty("paths").GetProperty("/api/health").TryGetProperty("get", out _));
    }

    [Fact]
    public async Task UnexpectedErrorsReturnGenericJsonWithoutExceptionDetails()
    {
        await using var factory = CreateFactory().WithWebHostBuilder(builder =>
            builder.ConfigureTestServices(services =>
                services.Replace(ServiceDescriptor.Singleton<TimeProvider>(new FailingClock()))));
        using var client = factory.CreateClient();
        using var response = await client.GetAsync("/api/health");
        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Single(json.RootElement.EnumerateObject());
        Assert.Equal("Internal Server Error", json.RootElement.GetProperty("error").GetString());
    }

    private sealed class FailingClock : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => throw new InvalidOperationException("Internal test detail");
    }

    [Theory]
    [InlineData("Development", "/unknown")]
    [InlineData("Production", "/swagger/index.html")]
    [InlineData("Production", "/swagger/v1/swagger.json")]
    public async Task MissingRoutesReturnReferenceErrorFormat(string environment, string path)
    {
        await using var factory = CreateFactory(environment);
        using var client = factory.CreateClient();
        using var response = await client.GetAsync(path);
        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Not Found", json.RootElement.GetProperty("error").GetString());
    }
}
