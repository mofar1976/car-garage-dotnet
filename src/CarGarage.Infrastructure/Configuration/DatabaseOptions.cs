using Npgsql;

namespace CarGarage.Infrastructure.Configuration;

public sealed class DatabaseOptions
{
    public const string SectionName = "Database";
    public string ConnectionString { get; init; } = "";

    public bool IsValid()
    {
        if (string.IsNullOrWhiteSpace(ConnectionString)) return false;
        try
        {
            var parsed = new NpgsqlConnectionStringBuilder(ConnectionString);
            return !string.IsNullOrWhiteSpace(parsed.Host)
                && !string.IsNullOrWhiteSpace(parsed.Database)
                && !string.IsNullOrWhiteSpace(parsed.Username);
        }
        catch (ArgumentException)
        {
            return false;
        }
    }
}
