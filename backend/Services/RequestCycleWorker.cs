namespace HrETracker.Services;
public class RequestCycleWorker(IServiceScopeFactory scopes, ILogger<RequestCycleWorker> logger, TimeProvider clock) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        using var timer = new PeriodicTimer(TimeSpan.FromSeconds(10), clock);
        do
        {
            try
            {
                using var scope = scopes.CreateScope();
                var created = await scope.ServiceProvider.GetRequiredService<IRequestCycleService>().CreateDueRequestsAsync(ct);
                logger.LogInformation("Due coat request cycle completed; {Count} request(s) created.", created);
            }
            catch (OperationCanceledException) when (ct.IsCancellationRequested) { break; }
            catch (Exception ex) { logger.LogError(ex, "Due coat request cycle failed; retrying in ten seconds."); }
        } while (await timer.WaitForNextTickAsync(ct));
    }
}
