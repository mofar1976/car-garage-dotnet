namespace CarGarage.Api.Contracts;

public sealed record HealthResponse(string Status, string Service, DateTimeOffset Timestamp);
