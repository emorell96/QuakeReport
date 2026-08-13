using Microsoft.EntityFrameworkCore;
using QuakeReport.Data;

namespace QuakeReport.ApiService.Persistence;

public sealed class RuntimeQuakeReportDbContextFactory(IConfiguration configuration)
    : IDbContextFactory<QuakeReportDbContext>
{
    public QuakeReportDbContext CreateDbContext()
    {
        var connectionString = configuration.GetConnectionString("quakereportdb") ??
            throw new InvalidOperationException("The quakereportdb connection string is not configured.");
        var options = new DbContextOptionsBuilder<QuakeReportDbContext>()
            .UseNpgsql(connectionString, npgsql => npgsql.UseNetTopologySuite())
            .Options;
        return new QuakeReportDbContext(options);
    }
}
