using CarGarage.Domain.Entities;

namespace CarGarage.Application.Abstractions;

public interface ITokenIssuer
{
    string CreateAccessToken(User user);
    int AccessLifetimeSeconds { get; }
    TimeSpan RefreshLifetime { get; }
}
