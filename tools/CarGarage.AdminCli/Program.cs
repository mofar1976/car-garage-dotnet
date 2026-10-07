using CarGarage.Application.Exceptions;
using CarGarage.Application.Models;
using CarGarage.Application.Services;
using CarGarage.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

if (args.Length is < 1 or > 3)
{
    Console.Error.WriteLine("Usage: CarGarage.AdminCli <email> [firstName] [lastName]. Password is read from the terminal or stdin.");
    return 1;
}

try
{
    var builder = Host.CreateApplicationBuilder();
    builder.Services.AddInfrastructure();
    using var host = builder.Build();
    await using var scope = host.Services.CreateAsyncScope();
    var registration = scope.ServiceProvider.GetRequiredService<UserRegistrationService>();
    var user = await registration.CreateAdminAsync(new RegisterInput
    {
        Email = args[0], Password = ReadPassword(),
        FirstName = args.ElementAtOrDefault(1) ?? "Platform",
        LastName = args.ElementAtOrDefault(2) ?? "Admin"
    });
    Console.WriteLine($"Created admin {user.Email} ({user.Id})");
    return 0;
}
catch (AppException error)
{
    Console.Error.WriteLine(error.Message);
    return 1;
}
catch (Exception)
{
    Console.Error.WriteLine("Admin creation failed. Check database configuration, connectivity and applied migrations.");
    return 1;
}

static string ReadPassword()
{
    if (Console.IsInputRedirected) return Console.ReadLine() ?? "";
    Console.Write("Password: ");
    var password = new System.Text.StringBuilder();
    while (true)
    {
        var key = Console.ReadKey(intercept: true);
        if (key.Key == ConsoleKey.Enter) { Console.WriteLine(); return password.ToString(); }
        if (key.Key == ConsoleKey.Backspace)
        {
            if (password.Length > 0) password.Length--;
        }
        else if (!char.IsControl(key.KeyChar)) password.Append(key.KeyChar);
    }
}
