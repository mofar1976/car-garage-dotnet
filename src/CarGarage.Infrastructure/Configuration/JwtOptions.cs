using System.Text;

namespace CarGarage.Infrastructure.Configuration;

public sealed class JwtOptions
{
    public const string SectionName = "Jwt";
    public string Secret { get; init; } = "";
    public string Issuer { get; init; } = "car-garage";
    public int AccessTtlSeconds { get; init; } = 900;
    public int RefreshTtlDays { get; init; } = 30;

    public bool IsValid() => Encoding.UTF8.GetByteCount(Secret) >= 32
        && !string.IsNullOrWhiteSpace(Issuer) && AccessTtlSeconds is > 0 and <= 86400
        && RefreshTtlDays is > 0 and <= 365;
}
