using Microsoft.EntityFrameworkCore;
using Npgsql;
using QuakeReport.Data;

namespace QuakeReport.ApiService.Persistence;

public sealed class RuntimeQuakeReportDbContextFactory(NpgsqlDataSource dataSource)
    : IDbContextFactory<QuakeReportDbContext>
{
    public QuakeReportDbContext CreateDbContext()
    {
        var options = new DbContextOptionsBuilder<QuakeReportDbContext>()
            .UseNpgsql(dataSource, npgsql => npgsql.UseNetTopologySuite())
            .Options;
        return new QuakeReportDbContext(options);
    }
}
