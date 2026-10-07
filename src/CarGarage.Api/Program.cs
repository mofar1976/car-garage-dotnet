using CarGarage.Api.Authentication;
using Microsoft.OpenApi;
using System.Text.Json.Serialization;
using CarGarage.Infrastructure;
using CarGarage.Infrastructure.Configuration;
using CarGarage.Infrastructure.Persistence;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using CarGarage.Api.Configuration;
using CarGarage.Api.Middleware;
using Microsoft.AspNetCore.Mvc;

var builder = WebApplication.CreateBuilder(args.Where(arg => arg != "--migrate").ToArray());

builder.Logging.ClearProviders();
builder.Logging.AddJsonConsole();
builder.Services.AddOptions<ServiceOptions>()
    .BindConfiguration(ServiceOptions.SectionName)
    .ValidateDataAnnotations()
    .ValidateOnStart();
builder.Services.AddSingleton(TimeProvider.System);
builder.Services.AddInfrastructure();
if (!args.Contains("--migrate", StringComparer.Ordinal)) builder.Services.AddPortalAuthentication();
builder.Services.AddControllers().AddJsonOptions(options =>
    options.JsonSerializerOptions.DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull)
    .ConfigureApiBehaviorOptions(options =>
{
    options.InvalidModelStateResponseFactory = context => new BadRequestObjectResult(new
    {
        error = "ValidationError",
        details = new
        {
            formErrors = Array.Empty<string>(),
            fieldErrors = context.ModelState.Where(entry => entry.Value?.Errors.Count > 0)
                .GroupBy(entry => string.IsNullOrEmpty(entry.Key) ? "body" : char.ToLowerInvariant(entry.Key[0]) + entry.Key[1..])
                .ToDictionary(group => group.Key, group => group.SelectMany(entry =>
                    entry.Value!.Errors.Select(error => error.ErrorMessage)).ToArray())
        }
    });
});
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(options =>
{
    options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Type = SecuritySchemeType.Http, Scheme = "bearer", BearerFormat = "JWT",
        Description = "Access token for the required portal (ADMIN, WORKSHOP or CUSTOMER)."
    });
    options.OperationFilter<SwaggerAuthOperationFilter>();
});

var app = builder.Build();
if (args.Contains("--migrate", StringComparer.Ordinal))
{
    // Explicit migration mode exits without starting the HTTP server.
    _ = app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
    await using var scope = app.Services.CreateAsyncScope();
    await scope.ServiceProvider.GetRequiredService<CarGarageDbContext>().Database.MigrateAsync();
    app.Logger.LogInformation("Database migrations applied successfully");
    await app.DisposeAsync();
    return;
}

app.UseMiddleware<ExceptionHandlingMiddleware>();
app.UseStatusCodePages(async context =>
{
    var response = context.HttpContext.Response;
    await response.WriteAsJsonAsync(new
    {
        error = response.StatusCode == 404 ? "Not Found" : "Request failed"
    });
});

if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.UseAuthentication();
app.UseAuthorization();
app.MapControllers();
app.Run();

public partial class Program;
