namespace QuakeReport.Data.Models;
public class BloodDonationCenterAbuseReport { public Guid Id {get;set;} public required Guid BloodDonationCenterId {get;set;} public BloodDonationCenter? BloodDonationCenter {get;set;} public required string Reason {get;set;} public string? Details {get;set;} public DateTimeOffset CreatedAt {get;set;}=DateTimeOffset.UtcNow; }
