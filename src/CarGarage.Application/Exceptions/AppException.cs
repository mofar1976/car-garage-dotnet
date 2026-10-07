namespace CarGarage.Application.Exceptions;

public enum AppError { Validation, Unauthorized, Conflict }

public sealed class AppException(AppError kind, string message, object? details = null) : Exception(message)
{
    public AppError Kind { get; } = kind;
    public object? Details { get; } = details;
}
