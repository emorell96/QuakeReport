using Microsoft.EntityFrameworkCore;
using QuakeReport.Data.Models;

namespace QuakeReport.Data;

public class QuakeReportDbContext(DbContextOptions<QuakeReportDbContext> options) : DbContext(options)
{
    public DbSet<Earthquake> Earthquakes => Set<Earthquake>();

    public DbSet<DamageReport> DamageReports => Set<DamageReport>();

    public DbSet<ReportMedia> ReportMedia => Set<ReportMedia>();

    /// <summary>
    /// The single event this MVP currently reports against. Referenced by the
    /// seed data below and available for the app layer to read by well-known id.
    /// </summary>
    public static readonly Guid ColombiaEarthquakeId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.Entity<Earthquake>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.Source).HasMaxLength(200);

            entity.HasData(new Earthquake
            {
                Id = ColombiaEarthquakeId,
                Name = "M7.4 - Colombia",
                Magnitude = 7.4,
                OccurredAt = new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero),
                EpicenterLatitude = 4.5709,
                EpicenterLongitude = -74.2973,
                Source = null,
                CreatedAt = new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero)
            });
        });

        modelBuilder.Entity<DamageReport>(entity =>
        {
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.Address).HasMaxLength(300);
            entity.HasIndex(e => e.Severity);

            entity.HasMany(e => e.Media)
                .WithOne(m => m.DamageReport)
                .HasForeignKey(m => m.DamageReportId)
                .OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<ReportMedia>(entity =>
        {
            entity.Property(e => e.BlobUrl).HasMaxLength(1000);
            entity.Property(e => e.FileName).HasMaxLength(300);
            entity.Property(e => e.ContentType).HasMaxLength(100);
        });
    }
}
