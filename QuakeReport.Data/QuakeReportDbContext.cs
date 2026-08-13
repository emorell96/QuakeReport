using Microsoft.EntityFrameworkCore;
using QuakeReport.Data.Geospatial;
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
    public DbSet<CollectionPoint> CollectionPoints => Set<CollectionPoint>();
    public DbSet<CollectionPointComment> CollectionPointComments => Set<CollectionPointComment>();
    public DbSet<CollectionPointAbuseReport> CollectionPointAbuseReports => Set<CollectionPointAbuseReport>();
    public DbSet<Shelter> Shelters => Set<Shelter>();
    public DbSet<ShelterAbuseReport> ShelterAbuseReports => Set<ShelterAbuseReport>();
    public DbSet<HelpRequest> HelpRequests => Set<HelpRequest>();
    public DbSet<HelpRequestComment> HelpRequestComments => Set<HelpRequestComment>();
    public DbSet<HelpRequestAbuseReport> HelpRequestAbuseReports => Set<HelpRequestAbuseReport>();
    public DbSet<BloodDonationCenter> BloodDonationCenters => Set<BloodDonationCenter>();
    public DbSet<BloodDonationCenterComment> BloodDonationCenterComments => Set<BloodDonationCenterComment>();
    public DbSet<BloodDonationCenterAbuseReport> BloodDonationCenterAbuseReports => Set<BloodDonationCenterAbuseReport>();
    public DbSet<IngestionSubmission> IngestionSubmissions => Set<IngestionSubmission>();
    public DbSet<GeocodingReviewItem> GeocodingReviewItems => Set<GeocodingReviewItem>();

    /// <summary>
    /// The single event this MVP currently reports against. Referenced by the
    /// seed data below and available for the app layer to read by well-known id.
    /// </summary>
    public static readonly Guid ColombiaEarthquakeId = Guid.Parse("11111111-1111-1111-1111-111111111111");

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.HasPostgresExtension("postgis");

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
                Location = GeoPoint.FromCoordinates(4.5709, -74.2973),
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

        modelBuilder.Entity<CollectionPoint>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(200);
            entity.Property(e => e.OrganizationName).HasMaxLength(200);
            entity.Property(e => e.Address).HasMaxLength(400);
            entity.Property(e => e.SearchText).HasMaxLength(1200);
            entity.Property(e => e.Description).HasMaxLength(3000);
            entity.Property(e => e.NeedsSummary).HasMaxLength(2000);
            entity.Property(e => e.ReceivingInstructions).HasMaxLength(2000);
            entity.Property(e => e.ContactName).HasMaxLength(200);
            entity.Property(e => e.ContactPhone).HasMaxLength(80);
            entity.Property(e => e.ContactWhatsApp).HasMaxLength(80);
            entity.Property(e => e.ContactEmail).HasMaxLength(320);
            entity.Property(e => e.ManagementCodeHash).HasMaxLength(64);
            entity.Property(e => e.ModeratedBy).HasMaxLength(320);
            entity.HasIndex(e => new { e.EarthquakeId, e.ModerationStatus, e.OperationalStatus, e.CreatedAt });
            entity.HasIndex(e => new { e.EarthquakeId, e.OperationalStatus, e.UpdatedAt });
            entity.HasIndex(e => e.SearchText);
            entity.HasIndex(e => e.ManagementCodeHash).IsUnique();
            entity.HasOne(e => e.Earthquake).WithMany().HasForeignKey(e => e.EarthquakeId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<CollectionPointComment>(entity =>
        {
            entity.Property(e => e.DisplayName).HasMaxLength(100);
            entity.Property(e => e.Message).HasMaxLength(2000);
            entity.HasIndex(e => new { e.CollectionPointId, e.CreatedAt });
            entity.HasOne(e => e.CollectionPoint).WithMany(e => e.Comments).HasForeignKey(e => e.CollectionPointId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<CollectionPointAbuseReport>(entity =>
        {
            entity.Property(e => e.Reason).HasMaxLength(200);
            entity.Property(e => e.Details).HasMaxLength(2000);
            entity.HasIndex(e => new { e.CollectionPointId, e.CreatedAt });
            entity.HasOne(e => e.CollectionPoint).WithMany(e => e.AbuseReports).HasForeignKey(e => e.CollectionPointId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<Shelter>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
            entity.Property(e => e.OrganizationName).HasMaxLength(200);
            entity.Property(e => e.Address).HasMaxLength(400).IsRequired();
            entity.Property(e => e.SearchText).HasMaxLength(1200);
            entity.Property(e => e.Description).HasMaxLength(3000).IsRequired();
            entity.Property(e => e.OperatingInstructions).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.ContactName).HasMaxLength(200);
            entity.Property(e => e.ContactPhone).HasMaxLength(80);
            entity.Property(e => e.ContactWhatsApp).HasMaxLength(80);
            entity.Property(e => e.ContactEmail).HasMaxLength(320);
            entity.Property(e => e.ManagementCodeHash).HasMaxLength(64);
            entity.Property(e => e.ModeratedBy).HasMaxLength(320);
            entity.HasIndex(e => new { e.EarthquakeId, e.ModerationStatus, e.OperationalStatus, e.CreatedAt });
            entity.HasIndex(e => new { e.EarthquakeId, e.OperationalStatus, e.UpdatedAt });
            entity.HasIndex(e => e.SearchText);
            entity.HasIndex(e => e.ManagementCodeHash).IsUnique();
            entity.HasOne(e => e.Earthquake).WithMany().HasForeignKey(e => e.EarthquakeId).OnDelete(DeleteBehavior.Restrict);
        });

        modelBuilder.Entity<ShelterAbuseReport>(entity =>
        {
            entity.Property(e => e.Reason).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Details).HasMaxLength(2000);
            entity.HasIndex(e => new { e.ShelterId, e.CreatedAt });
            entity.HasOne(e => e.Shelter).WithMany(e => e.AbuseReports).HasForeignKey(e => e.ShelterId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<HelpRequest>(entity =>
        {
            entity.Property(e => e.Title).HasMaxLength(200).IsRequired();
            entity.Property(e => e.RequesterName).HasMaxLength(200).IsRequired();
            entity.Property(e => e.OrganizationName).HasMaxLength(200);
            entity.Property(e => e.Address).HasMaxLength(400).IsRequired();
            entity.Property(e => e.SearchText).HasMaxLength(1800);
            entity.Property(e => e.NeedDetails).HasMaxLength(3000).IsRequired();
            entity.Property(e => e.Instructions).HasMaxLength(2000);
            entity.Property(e => e.PublicPhone).HasMaxLength(80).IsRequired();
            entity.Property(e => e.PublicWhatsApp).HasMaxLength(80);
            entity.Property(e => e.PublicEmail).HasMaxLength(320);
            entity.Property(e => e.ManagementCodeHash).HasMaxLength(64);
            entity.Property(e => e.ModeratedBy).HasMaxLength(320);
            entity.HasIndex(e => new { e.EarthquakeId, e.ModerationStatus, e.Status, e.Priority, e.CreatedAt });
            entity.HasIndex(e => new { e.EarthquakeId, e.Status, e.UpdatedAt });
            entity.HasIndex(e => e.SearchText);
            entity.HasIndex(e => e.ManagementCodeHash).IsUnique();
            entity.HasOne(e => e.Earthquake).WithMany().HasForeignKey(e => e.EarthquakeId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<HelpRequestComment>(entity =>
        {
            entity.Property(e => e.DisplayName).HasMaxLength(100);
            entity.Property(e => e.Message).HasMaxLength(2000).IsRequired();
            entity.HasIndex(e => new { e.HelpRequestId, e.CreatedAt });
            entity.HasOne(e => e.HelpRequest).WithMany(e => e.Comments).HasForeignKey(e => e.HelpRequestId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<HelpRequestAbuseReport>(entity =>
        {
            entity.Property(e => e.Reason).HasMaxLength(200).IsRequired();
            entity.Property(e => e.Details).HasMaxLength(2000);
            entity.HasIndex(e => new { e.HelpRequestId, e.CreatedAt });
            entity.HasOne(e => e.HelpRequest).WithMany(e => e.AbuseReports).HasForeignKey(e => e.HelpRequestId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<BloodDonationCenter>(entity =>
        {
            entity.Property(e => e.Name).HasMaxLength(200).IsRequired();
        entity.Property(e => e.OrganizationName).HasMaxLength(200);
        entity.Property(e => e.Address).HasMaxLength(400).IsRequired();
        entity.Property(e => e.SearchText).HasMaxLength(2200);
        entity.Property(e => e.Description).HasMaxLength(3000);
        entity.Property(e => e.OperatingInstructions).HasMaxLength(2500).IsRequired();
        entity.Property(e => e.NeedsSummary).HasMaxLength(2000).IsRequired();
        entity.Property(e => e.PublicPhone).HasMaxLength(80).IsRequired();
        entity.Property(e => e.PublicWhatsApp).HasMaxLength(80);
        entity.Property(e => e.PublicEmail).HasMaxLength(320);
        entity.Property(e => e.ManagementCodeHash).HasMaxLength(64);
        entity.Property(e => e.ModeratedBy).HasMaxLength(320);
            entity.HasIndex(e => new { e.EarthquakeId, e.ModerationStatus, e.OperationalStatus, e.CenterType, e.CreatedAt });
        entity.HasIndex(e => new { e.EarthquakeId, e.OperationalStatus, e.UpdatedAt });
        entity.HasIndex(e => new { e.EarthquakeId, e.BloodTypes, e.Components });
        entity.HasIndex(e => e.SearchText);
        entity.HasIndex(e => e.ManagementCodeHash).IsUnique();
        entity.HasOne(e => e.Earthquake).WithMany().HasForeignKey(e => e.EarthquakeId).OnDelete(DeleteBehavior.Restrict);
        });
        modelBuilder.Entity<BloodDonationCenterComment>(entity => { entity.Property(e => e.DisplayName).HasMaxLength(100);
        entity.Property(e => e.Message).HasMaxLength(2000).IsRequired();
        entity.HasIndex(e => new { e.BloodDonationCenterId, e.CreatedAt });
        entity.HasOne(e => e.BloodDonationCenter).WithMany(e => e.Comments).HasForeignKey(e => e.BloodDonationCenterId).OnDelete(DeleteBehavior.Cascade);
        });
        modelBuilder.Entity<BloodDonationCenterAbuseReport>(entity => { entity.Property(e => e.Reason).HasMaxLength(200).IsRequired();
        entity.Property(e => e.Details).HasMaxLength(2000);
        entity.HasIndex(e => new { e.BloodDonationCenterId, e.CreatedAt });
        entity.HasOne(e => e.BloodDonationCenter).WithMany(e => e.AbuseReports).HasForeignKey(e => e.BloodDonationCenterId).OnDelete(DeleteBehavior.Cascade);
        });

        modelBuilder.Entity<IngestionSubmission>(entity =>
        {
            entity.Property(e => e.SourceUrl).HasMaxLength(2000).IsRequired();
            entity.Property(e => e.ExternalPostId).HasMaxLength(300);
            entity.Property(e => e.IdempotencyKeyHash).HasMaxLength(64).IsRequired();
            entity.Property(e => e.EvidenceSummary).HasMaxLength(1000);
            entity.HasIndex(e => new { e.EntityType, e.IdempotencyKeyHash }).IsUnique();
            entity.HasIndex(e => new { e.EntityType, e.Platform, e.ExternalPostId }).IsUnique().HasFilter("\"ExternalPostId\" IS NOT NULL");
            entity.HasIndex(e => new { e.EarthquakeId, e.CreatedAt });
            entity.HasOne(e => e.Earthquake).WithMany().HasForeignKey(e => e.EarthquakeId).OnDelete(DeleteBehavior.Restrict);
        });

        ConfigureLocation<Earthquake>(modelBuilder, required: true);
        ConfigureLocation<DamageReport>(modelBuilder, required: true);
        ConfigureLocation<MissingPersonLocation>(modelBuilder);
        ConfigureLocation<MissingPersonTip>(modelBuilder);
        ConfigureLocation<CollectionPoint>(modelBuilder);
        ConfigureLocation<Shelter>(modelBuilder);
        ConfigureLocation<HelpRequest>(modelBuilder);
        ConfigureLocation<BloodDonationCenter>(modelBuilder);

        modelBuilder.Entity<GeocodingReviewItem>(entity =>
        {
            entity.Property(item => item.EntityType).HasMaxLength(100);
            entity.Property(item => item.AddressSnapshot).HasMaxLength(500);
            entity.Property(item => item.AddressHash).HasMaxLength(64);
            entity.Property(item => item.Reason).HasMaxLength(500);
            entity.Property(item => item.FormattedAddress).HasMaxLength(500);
            entity.Property(item => item.GooglePlaceId).HasMaxLength(300);
            entity.Property(item => item.Granularity).HasMaxLength(50);
            entity.Property(item => item.ResolvedBy).HasMaxLength(320);
            entity.Property(item => item.CandidateLocation).HasColumnType("geography (point, 4326)");
            entity.HasIndex(item => new { item.EntityType, item.EntityId, item.AddressHash }).IsUnique();
            entity.HasIndex(item => new { item.Status, item.LastAttemptAt });
        });
    }

    private static void ConfigureLocation<TEntity>(ModelBuilder modelBuilder, bool required = false)
        where TEntity : class, IEntityWithLocation
    {
        var entity = modelBuilder.Entity<TEntity>();
        entity.Property(item => item.Location)
            .HasColumnType("geography (point, 4326)")
            .IsRequired(required);
        entity.HasIndex(item => item.Location).HasMethod("gist");
    }
}
