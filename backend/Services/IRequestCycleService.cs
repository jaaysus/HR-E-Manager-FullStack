using HrETracker.Models;
namespace HrETracker.Services;
public interface IRequestCycleService
{
    Task<int> CreateDueRequestsAsync(CancellationToken ct);
    Task<RequestPage> GetAsync(CoatRequestStatus? status, Guid? departmentId, int page, int pageSize, CancellationToken ct);
    Task<CoatRequestResponse?> GetByIdAsync(Guid id, CancellationToken ct);
    Task<CoatRequestResponse?> ProvideAsync(Guid id, RequestMutation input, CancellationToken ct);
    Task<CoatRequestResponse?> CancelAsync(Guid id, RequestMutation input, CancellationToken ct);
}
