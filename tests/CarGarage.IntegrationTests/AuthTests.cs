using System.IdentityModel.Tokens.Jwt;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Security.Claims;
using System.Text;
using System.Text.Json;
using CarGarage.Application.Abstractions;
using CarGarage.Application.Models;
using Microsoft.Extensions.DependencyInjection;
using CarGarage.Application.Services;
using CarGarage.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.IdentityModel.Tokens;
using Xunit;

namespace CarGarage.IntegrationTests;

[Trait("Category", "Database")]
public sealed class AuthTests(AuthFixture fixture) : IClassFixture<AuthFixture>
{
    private const string Password = "Test-password123!";
    private static string Email() => $"{Guid.NewGuid():N}@example.test";
    private static object Registration(string email, string password = Password) => new
    {
        email, password, firstName = "Carla", lastName = "Customer"
    };
    private async Task<AuthResponse> Register(HttpClient client, string? email = null)
    {
        using var response = await client.PostAsJsonAsync("/api/auth/customer/register", Registration(email ?? Email()));
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }
    private static void Bearer(HttpClient client, string token) => client.DefaultRequestHeaders.Authorization = new("Bearer", token);
    private async Task<AuthResponse> Admin(HttpClient client)
    {
        var email = Email();
        await fixture.CreateAdminAsync(email, Password);
        using var response = await client.PostAsJsonAsync("/api/auth/admin/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        return (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
    }

    [Fact]
    public async Task RegistrationReturnsReferenceContractWithoutSecretsOrPrivilegeEscalation()
    {
        using var client = fixture.Factory.CreateClient();
        using var response = await client.PostAsJsonAsync("/api/auth/customer/register", new
        {
            email = Email(), password = Password, firstName = "Carla", lastName = "Customer",
            audience = "ADMIN", roles = new[] { "PLATFORM_ADMIN" }
        });
        Assert.Equal(HttpStatusCode.Created, response.StatusCode);
        using var json = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var user = json.RootElement.GetProperty("user");
        Assert.Equal("CUSTOMER", user.GetProperty("audience").GetString());
        Assert.Equal("CUSTOMER", user.GetProperty("roles")[0].GetString());
        Assert.False(user.TryGetProperty("passwordHash", out _));
        Assert.False(user.TryGetProperty("phone", out _));
        var result = (await response.Content.ReadFromJsonAsync<AuthResponse>())!;
        var jwt = new JwtSecurityTokenHandler().ReadJwtToken(result.Tokens.AccessToken);
        Assert.Equal("HS256", jwt.Header.Alg);
        Assert.Equal("car-garage", jwt.Issuer);
        Assert.Equal("CUSTOMER", Assert.Single(jwt.Audiences));
        Assert.Equal(result.User.Id.ToString(), jwt.Subject);
        Assert.Equal(900, result.Tokens.ExpiresIn);
        Assert.Equal(TimeSpan.FromSeconds(900), jwt.ValidTo - jwt.IssuedAt);
        await using var db = fixture.Database.CreateContext();
        var storedUser = await db.Users.SingleAsync(item => item.Id == result.User.Id);
        Assert.NotEqual(Password, storedUser.PasswordHash);
        Assert.True(BCrypt.Net.BCrypt.Verify(Password, storedUser.PasswordHash));
        var storedToken = await db.RefreshTokens.SingleAsync(item => item.UserId == result.User.Id);
        Assert.Equal(AuthService.HashToken(result.Tokens.RefreshToken), storedToken.TokenHash);
        Assert.NotEqual(result.Tokens.RefreshToken, storedToken.TokenHash);
    }

    [Fact]
    public async Task DuplicateAndConcurrentRegistrationReturnConflict()
    {
        using var client = fixture.Factory.CreateClient();
        var email = Email();
        var responses = await Task.WhenAll(
            client.PostAsJsonAsync("/api/auth/customer/register", Registration(email)),
            client.PostAsJsonAsync("/api/auth/customer/register", Registration(email.ToUpperInvariant())));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Created);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Conflict);
        foreach (var response in responses) response.Dispose();
        await using var db = fixture.Database.CreateContext();
        var id = (await db.Users.SingleAsync(user => user.Email == email)).Id;
        Assert.Single(await db.RefreshTokens.Where(token => token.UserId == id).ToListAsync());
    }

    [Fact]
    public async Task InvalidAndOverlongBcryptInputReturnValidationErrors()
    {
        using var client = fixture.Factory.CreateClient();
        using var invalid = await client.PostAsJsonAsync("/api/auth/customer/register", new { email = "bad", password = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, invalid.StatusCode);
        using var json = JsonDocument.Parse(await invalid.Content.ReadAsStringAsync());
        Assert.Equal("ValidationError", json.RootElement.GetProperty("error").GetString());
        Assert.True(json.RootElement.GetProperty("details").TryGetProperty("fieldErrors", out _));
        using var longPassword = await client.PostAsJsonAsync("/api/auth/customer/register", Registration(Email(), new string('é', 40)));
        Assert.Equal(HttpStatusCode.BadRequest, longPassword.StatusCode);
        using var shortRefresh = await client.PostAsJsonAsync("/api/auth/customer/refresh", new { refreshToken = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, shortRefresh.StatusCode);
    }

    [Fact]
    public async Task LoginAndMeWorkAndInvalidCredentialsAreRejected()
    {
        using var client = fixture.Factory.CreateClient();
        var registered = await Register(client);
        using var login = await client.PostAsJsonAsync("/api/auth/customer/login", new { email = registered.User.Email.ToUpperInvariant(), password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var result = (await login.Content.ReadFromJsonAsync<AuthResponse>())!;
        Bearer(client, result.Tokens.AccessToken);
        var me = await client.GetFromJsonAsync<AuthUser>("/api/auth/customer/me");
        Assert.Equal(registered.User.Id, me!.Id);
        foreach (var input in new[] { new { email = registered.User.Email, password = "wrong" }, new { email = Email(), password = Password } })
        {
            using var wrong = await client.PostAsJsonAsync("/api/auth/customer/login", input);
            Assert.Equal(HttpStatusCode.Unauthorized, wrong.StatusCode);
        }
        using var wrongPortal = await client.PostAsJsonAsync("/api/auth/workshop/login", new { email = registered.User.Email, password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongPortal.StatusCode);
    }

    [Theory]
    [InlineData("admin")]
    [InlineData("workshop")]
    public async Task ProtectedRegistrationRequiresPlatformAdminAndEachPortalCanLogin(string portal)
    {
        using var client = fixture.Factory.CreateClient();
        var email = Email();
        using var anonymous = await client.PostAsJsonAsync($"/api/auth/{portal}/register", Registration(email));
        Assert.Equal(HttpStatusCode.Unauthorized, anonymous.StatusCode);
        var customer = await Register(client, email);
        Bearer(client, customer.Tokens.AccessToken);
        using var customerAttempt = await client.PostAsJsonAsync($"/api/auth/{portal}/register", Registration(email));
        Assert.Equal(HttpStatusCode.Unauthorized, customerAttempt.StatusCode);
        var admin = await Admin(client);
        Bearer(client, admin.Tokens.AccessToken);
        using var created = await client.PostAsJsonAsync($"/api/auth/{portal}/register", Registration(email));
        Assert.Equal(HttpStatusCode.Created, created.StatusCode);
        var account = (await created.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.Equal(portal.ToUpperInvariant(), account.User.Audience);
        Assert.Equal(portal == "admin" ? "PLATFORM_ADMIN" : "WORKSHOP_MEMBER", Assert.Single(account.User.Roles));
        using var login = await client.PostAsJsonAsync($"/api/auth/{portal}/login", new { email, password = Password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        Bearer(client, (await login.Content.ReadFromJsonAsync<AuthResponse>())!.Tokens.AccessToken);
        Assert.Equal(account.User.Id, (await client.GetFromJsonAsync<AuthUser>($"/api/auth/{portal}/me"))!.Id);
    }

    [Fact]
    public async Task TokensCannotCrossPortalsAndMissingOrInvalidTokensReturnJson401()
    {
        using var client = fixture.Factory.CreateClient();
        using var missing = await client.GetAsync("/api/auth/customer/me");
        Assert.Equal(HttpStatusCode.Unauthorized, missing.StatusCode);
        var customer = await Register(client);
        Bearer(client, customer.Tokens.AccessToken);
        using var workshop = await client.GetAsync("/api/auth/workshop/me");
        Assert.Equal(HttpStatusCode.Unauthorized, workshop.StatusCode);
        var admin = await Admin(client);
        Bearer(client, admin.Tokens.AccessToken);
        using var wrongCustomer = await client.GetAsync("/api/auth/customer/me");
        Assert.Equal(HttpStatusCode.Unauthorized, wrongCustomer.StatusCode);
        Bearer(client, "invalid-token");
        using var invalid = await client.GetAsync("/api/auth/customer/me");
        Assert.Equal(HttpStatusCode.Unauthorized, invalid.StatusCode);
        using var json = JsonDocument.Parse(await invalid.Content.ReadAsStringAsync());
        Assert.True(json.RootElement.TryGetProperty("error", out _));
    }

    [Theory]
    [InlineData("expired")]
    [InlineData("issuer")]
    [InlineData("signature")]
    [InlineData("algorithm")]
    public async Task JwtValidationRejectsInvalidLifetimeIssuerSignatureAndAlgorithm(string failure)
    {
        using var client = fixture.Factory.CreateClient();
        var account = await Register(client);
        var now = DateTime.UtcNow;
        var token = new JwtSecurityToken(
            failure == "issuer" ? "wrong-issuer" : "car-garage", "CUSTOMER",
            [new Claim("sub", account.User.Id.ToString()), new Claim("roles", "CUSTOMER")],
            now.AddHours(-2), failure == "expired" ? now.AddHours(-1) : now.AddMinutes(15),
            new SigningCredentials(new SymmetricSecurityKey(Encoding.UTF8.GetBytes(
                failure == "signature" ? new string('x', 64) : AuthFixture.Secret)),
                failure == "algorithm" ? SecurityAlgorithms.HmacSha384 : SecurityAlgorithms.HmacSha256));
        Bearer(client, new JwtSecurityTokenHandler().WriteToken(token));
        using var response = await client.GetAsync("/api/auth/customer/me");
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task RefreshRotatesOnceAndWrongAudienceDoesNotConsumeToken()
    {
        using var client = fixture.Factory.CreateClient();
        var account = await Register(client);
        using var wrongAudience = await client.PostAsJsonAsync("/api/auth/workshop/refresh", new { refreshToken = account.Tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, wrongAudience.StatusCode);
        using var refreshed = await client.PostAsJsonAsync("/api/auth/customer/refresh", new { refreshToken = account.Tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refreshed.StatusCode);
        var rotated = (await refreshed.Content.ReadFromJsonAsync<AuthResponse>())!;
        Assert.NotEqual(account.Tokens.RefreshToken, rotated.Tokens.RefreshToken);
        using var reused = await client.PostAsJsonAsync("/api/auth/customer/refresh", new { refreshToken = account.Tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reused.StatusCode);
        Bearer(client, rotated.Tokens.AccessToken);
        Assert.Equal(account.User.Id, (await client.GetFromJsonAsync<AuthUser>("/api/auth/customer/me"))!.Id);
    }

    [Fact]
    public async Task ParallelRefreshHasExactlyOneWinner()
    {
        using var client = fixture.Factory.CreateClient();
        var account = await Register(client);
        var input = new { refreshToken = account.Tokens.RefreshToken };
        var responses = await Task.WhenAll(client.PostAsJsonAsync("/api/auth/customer/refresh", input),
            client.PostAsJsonAsync("/api/auth/customer/refresh", input));
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.OK);
        Assert.Single(responses, response => response.StatusCode == HttpStatusCode.Unauthorized);
        foreach (var response in responses) response.Dispose();
        await using var db = fixture.Database.CreateContext();
        Assert.Single(await db.RefreshTokens.Where(token => token.UserId == account.User.Id).ToListAsync());
    }

    [Fact]
    public async Task CleanupDeletesExpiredTokensAndPreservesValidTokens()
    {
        using var client = fixture.Factory.CreateClient();
        var expired = await Register(client);
        var valid = await Register(client);
        await using var db = fixture.Database.CreateContext();
        var now = DateTimeOffset.UtcNow;
        await db.RefreshTokens.Where(token => token.UserId == expired.User.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.ExpiresAt, now.AddMinutes(-1)));
        await using var scope = fixture.Factory.Services.CreateAsyncScope();
        await scope.ServiceProvider.GetRequiredService<IAuthStore>().DeleteExpiredRefreshTokensAsync(now, default);
        Assert.False(await db.RefreshTokens.AnyAsync(token => token.UserId == expired.User.Id));
        Assert.True(await db.RefreshTokens.AnyAsync(token => token.UserId == valid.User.Id));
    }

    [Fact]
    public async Task BcryptAccepts72BytesButDoesNotSilentlyAcceptPasswordSuffixes()
    {
        using var client = fixture.Factory.CreateClient();
        var email = Email();
        var password = new string('a', 72);
        using var register = await client.PostAsJsonAsync("/api/auth/customer/register", Registration(email, password));
        Assert.Equal(HttpStatusCode.Created, register.StatusCode);
        using var login = await client.PostAsJsonAsync("/api/auth/customer/login", new { email, password });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        using var suffix = await client.PostAsJsonAsync("/api/auth/customer/login", new { email, password = password + "suffix" });
        Assert.Equal(HttpStatusCode.Unauthorized, suffix.StatusCode);
    }

    [Fact]
    public async Task LogoutRevokesRefreshTokenAndIsIdempotent()
    {
        using var client = fixture.Factory.CreateClient();
        var account = await Register(client);
        for (var attempt = 0; attempt < 2; attempt++)
        {
            using var response = await client.PostAsJsonAsync("/api/auth/customer/logout", new { refreshToken = account.Tokens.RefreshToken });
            Assert.Equal(HttpStatusCode.NoContent, response.StatusCode);
            Assert.Equal("", await response.Content.ReadAsStringAsync());
        }
        using var refresh = await client.PostAsJsonAsync("/api/auth/customer/refresh", new { refreshToken = account.Tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task ExpiredRefreshTokenIsRejectedAndDeleted()
    {
        using var client = fixture.Factory.CreateClient();
        var account = await Register(client);
        await using var db = fixture.Database.CreateContext();
        await db.RefreshTokens.Where(token => token.UserId == account.User.Id)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.ExpiresAt, DateTimeOffset.UtcNow.AddMinutes(-1)));
        using var response = await client.PostAsJsonAsync("/api/auth/customer/refresh", new { refreshToken = account.Tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.False(await db.RefreshTokens.AnyAsync(token => token.UserId == account.User.Id));
    }

    [Theory]
    [InlineData(false)]
    [InlineData(true)]
    public async Task DisabledOrDeletedUsersCannotLoginRefreshOrUseAccessToken(bool deleted)
    {
        using var client = fixture.Factory.CreateClient();
        var account = await Register(client);
        await using var db = fixture.Database.CreateContext();
        var user = await db.Users.SingleAsync(user => user.Id == account.User.Id);
        if (deleted) user.Deleted = true;
        else user.IsActive = false;
        await db.SaveChangesAsync();
        Bearer(client, account.Tokens.AccessToken);
        using var me = await client.GetAsync("/api/auth/customer/me");
        Assert.Equal(HttpStatusCode.Unauthorized, me.StatusCode);
        using var login = await client.PostAsJsonAsync("/api/auth/customer/login", new { email = account.User.Email, password = Password });
        Assert.Equal(HttpStatusCode.Unauthorized, login.StatusCode);
        using var refresh = await client.PostAsJsonAsync("/api/auth/customer/refresh", new { refreshToken = account.Tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task RevokedAdminRoleTakesEffectForExistingToken()
    {
        using var client = fixture.Factory.CreateClient();
        var account = await Admin(client);
        await using var db = fixture.Database.CreateContext();
        var admin = await db.Users.SingleAsync(user => user.Id == account.User.Id);
        admin.Roles = [];
        await db.SaveChangesAsync();
        Bearer(client, account.Tokens.AccessToken);
        using var response = await client.PostAsJsonAsync("/api/auth/workshop/register", Registration(Email()));
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }

    [Fact]
    public async Task SwaggerListsAllAuthRoutesAndMarksOnlyProtectedOperations()
    {
        using var client = fixture.Factory.CreateClient();
        using var json = JsonDocument.Parse(await client.GetStringAsync("/swagger/v1/swagger.json"));
        var document = json.RootElement;
        Assert.Equal("bearer", document.GetProperty("components").GetProperty("securitySchemes").GetProperty("Bearer").GetProperty("scheme").GetString());
        var paths = document.GetProperty("paths");
        foreach (var portal in new[] { "customer", "workshop", "admin" })
        {
            Assert.True(paths.GetProperty($"/api/auth/{portal}/me").GetProperty("get").TryGetProperty("security", out _));
            foreach (var action in new[] { "register", "login", "refresh", "logout" })
                Assert.True(paths.GetProperty($"/api/auth/{portal}/{action}").TryGetProperty("post", out _));
        }
        Assert.False(paths.GetProperty("/api/auth/customer/login").GetProperty("post").TryGetProperty("security", out _));
    }
}
