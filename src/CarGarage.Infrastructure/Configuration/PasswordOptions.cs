namespace CarGarage.Infrastructure.Configuration;

public sealed class PasswordOptions
{
    public const string SectionName = "Password";
    public int WorkFactor { get; init; } = 10;
}
