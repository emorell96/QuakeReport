namespace QuakeReport.Data.Models;
public class BloodDonationCenterComment { public Guid Id {get;set;} public required Guid BloodDonationCenterId {get;set;} public BloodDonationCenter? BloodDonationCenter {get;set;} public string? DisplayName {get;set;} public required string Message {get;set;} public bool IsHidden {get;set;} public DateTimeOffset CreatedAt {get;set;}=DateTimeOffset.UtcNow; }
