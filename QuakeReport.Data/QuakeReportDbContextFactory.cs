using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;

namespace QuakeReport.Data;

// Keeps EF migration generation independent from Azure credentials and local Aspire startup.
public sealed class QuakeReportDbContextFactory : IDesignTimeDbContextFactory<QuakeReportDbContext>
{
    public QuakeReportDbContext CreateDbContext(string[] args)
    {
        var options = new DbContextOptionsBuilder<QuakeReportDbContext>()
            .UseNpgsql("Host=localhost;Port=5432;Database=quakereport;Username=postgres;Password=postgres")
            .Options;
        return new QuakeReportDbContext(options);
    }
}
