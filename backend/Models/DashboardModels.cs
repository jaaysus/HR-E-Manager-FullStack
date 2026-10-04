namespace HrETracker.Models;

public record MonthlyIssue(int Year, int Month, int Quantity);
public record DashboardResponse(DateOnly BusinessDate, string TimeZoneId, int ReportYear,
    int ActiveEmployees, int InactiveEmployees, int PendingRequests, int OutOfStockRequests,
    int ProvidedRequests, int CancelledRequests, long QuantityOnHand, int LowStockItems,
    int UnreadNotifications, IReadOnlyList<MonthlyIssue> MonthlyIssues);
