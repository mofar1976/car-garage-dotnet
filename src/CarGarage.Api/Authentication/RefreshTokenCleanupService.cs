using CarGarage.Application.Abstractions;

namespace CarGarage.Api.Authentication;

public sealed class RefreshTokenCleanupService(IServiceScopeFactory scopes, TimeProvider clock,
    ILogger<RefreshTokenCleanupService> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromHours(1));
        try
        {
            while (await timer.WaitForNextTickAsync(stoppingToken))
            {
                try
                {
                    await using var scope = scopes.CreateAsyncScope();
                    await scope.ServiceProvider.GetRequiredService<IAuthStore>()
                        .DeleteExpiredRefreshTokensAsync(clock.GetUtcNow(), stoppingToken);
                }
                catch (Exception error) when (error is not OperationCanceledException)
                {
                    logger.LogError(error, "Refresh-token cleanup failed");
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { }
    }
}
