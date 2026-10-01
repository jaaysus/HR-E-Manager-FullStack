using HrETracker.Models;

namespace HrETracker.Services;

public interface IRequestCycleService
{
    Task<int> CreateDueRequestsAsync(CancellationToken cancellationToken);
    Task<IReadOnlyList<CoatRequestResponse>> GetAsync(CoatRequestStatus? status, CancellationToken cancellationToken);
    Task<CoatRequestResponse?> ProvideAsync(Guid requestId, CancellationToken cancellationToken);
}
