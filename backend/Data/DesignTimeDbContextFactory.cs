using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace HrETracker.Data;

public class DesignTimeDbContextFactory : IDesignTimeDbContextFactory<HrETrackerDbContext>
{
    public HrETrackerDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<HrETrackerDbContext>()
            .UseSqlServer("Server=.\\SQLEXPRESS;Database=HrETracker;Trusted_Connection=True;TrustServerCertificate=True")
            .Options;
        return new HrETrackerDbContext(options);
    }
}
