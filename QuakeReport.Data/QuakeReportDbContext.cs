using Microsoft.EntityFrameworkCore;
using QuakeReport.Data.Models;

namespace QuakeReport.Data;

public class QuakeReportDbContext(DbContextOptions<QuakeReportDbContext> options) : DbContext(options)
{
    public DbSet<Earthquake> Earthquakes => Set<Earthquake>();

    public DbSet<DamageReport> DamageReports => Set<DamageReport>();

    public DbSet<ReportMedia> ReportMedia => Set<ReportMedia>();
    public DbSet<MissingPerson> MissingPeople => Set<MissingPerson>();
    public DbSet<MissingPersonLocation> MissingPersonLocations => Set<MissingPersonLocation>();
    public DbSet<MissingPersonTip> MissingPersonTips => Set<MissingPersonTip>();
    public DbSet<AbuseReport> AbuseReports => Set<AbuseReport>();

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
                IsActive = true,
                CreatedAt = new DateTimeOffset(2026, 8, 10, 0, 0, 0, TimeSpan.Zero)
            });
        });

        modelBuilder.Entity<DamageReport>(entity =>
        {
            entity.Property(e => e.Description).HasMaxLength(2000);
            entity.Property(e => e.Address).HasMaxLength(300);
            entity.HasIndex(e => new { e.CreatedAt, e.Id });
            entity.HasIndex(e => new { e.Severity, e.CreatedAt, e.Id });
            entity.HasIndex(e => e.EarthquakeId);

            entity.HasOne(e => e.Earthquake)
                .WithMany()
                .HasForeignKey(e => e.EarthquakeId)
                .OnDelete(DeleteBehavior.Restrict);

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

        modelBuilder.Entity<MissingPerson>(entity =>
        {
            entity.Property(e => e.FullName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.SearchName).HasMaxLength(200);
            entity.Property(e => e.Aliases).HasMaxLength(500);
            entity.Property(e => e.ApproximateAge).HasMaxLength(50);
            entity.Property(e => e.Description).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.PhysicalDescription).HasMaxLength(1000);
            entity.Property(e => e.ClothingDescription).HasMaxLength(1000);
            entity.Property(e => e.PhotoUrl).HasMaxLength(1000);
            entity.Property(e => e.IdentificationNumberHash).HasMaxLength(64);
            entity.Property(e => e.IdentificationLastFour).HasMaxLength(4);
            entity.Property(e => e.ManagementCodeHash).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => new { e.EarthquakeId, e.Status, e.CreatedAt });
            entity.HasIndex(e => new { e.EarthquakeId, e.IdentificationNumberHash }).IsUnique();
            entity.HasIndex(e => e.SearchName);
            entity.HasOne(e => e.Earthquake).WithMany().HasForeignKey(e => e.EarthquakeId).OnDelete(DeleteBehavior.Restrict);
            entity.HasMany(e => e.Locations).WithOne(e => e.MissingPerson).HasForeignKey(e => e.MissingPersonId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.Tips).WithOne(e => e.MissingPerson).HasForeignKey(e => e.MissingPersonId).OnDelete(DeleteBehavior.Cascade);
            entity.HasMany(e => e.AbuseReports).WithOne(e => e.MissingPerson).HasForeignKey(e => e.MissingPersonId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<MissingPersonLocation>(entity =>
        {
            entity.Property(e => e.Address).HasMaxLength(300).IsRequired();
            entity.Property(e => e.SearchAddress).HasMaxLength(300);
            entity.Property(e => e.Note).HasMaxLength(500);
            entity.HasIndex(e => e.SearchAddress);
        });

        modelBuilder.Entity<MissingPersonTip>(entity =>
        {
            entity.Property(e => e.Message).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.Address).HasMaxLength(300);
            entity.Property(e => e.ResponderName).HasMaxLength(200);
            entity.Property(e => e.ResponderPhone).HasMaxLength(50);
            entity.Property(e => e.ResponderEmail).HasMaxLength(320);
            entity.HasIndex(e => new { e.MissingPersonId, e.CreatedAt });
        });

        modelBuilder.Entity<AbuseReport>(entity =>
        {
            entity.Property(e => e.Reason).HasMaxLength(100);
            entity.Property(e => e.Details).HasMaxLength(1000);
            entity.HasIndex(e => new { e.MissingPersonId, e.CreatedAt });
        });
    }
}
