namespace HrETracker.Services;

public class RequestCycleWorker(IServiceScopeFactory scopeFactory, ILogger<RequestCycleWorker> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await RunCycleAsync(stoppingToken);
        using var timer = new PeriodicTimer(TimeSpan.FromHours(12));
        while (await timer.WaitForNextTickAsync(stoppingToken))
            await RunCycleAsync(stoppingToken);
    }

    private async Task RunCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = scopeFactory.CreateScope();
            var service = scope.ServiceProvider.GetRequiredService<IRequestCycleService>();
            var created = await service.CreateDueRequestsAsync(cancellationToken);
            logger.LogInformation("Due coat request cycle completed; {CreatedCount} request(s) created.", created);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested) { }
        catch (Exception exception)
        {
            logger.LogError(exception, "Due coat request cycle failed.");
        }
    }
}
