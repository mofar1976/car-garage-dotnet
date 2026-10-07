using System.ComponentModel.DataAnnotations;

namespace CarGarage.Api.Configuration;

public sealed class ServiceOptions
{
    public const string SectionName = "Service";

    [Required]
    public string Name { get; init; } = "car-garage-api";
}
