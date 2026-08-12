namespace QuakeReport.Web.Models;

public interface ITurnstileProtectedForm
{
    string TurnstileToken { get; set; }
}

public interface IPrivacyConsentForm
{
    bool PrivacyConsent { get; set; }
}

public interface IFormWithLocation
{
    string? Address { get; set; }
    double? Latitude { get; set; }
    double? Longitude { get; set; }
}

public interface IFormWithEmail { string? Email { get; set; } }
public interface IFormWithPhone { string? Phone { get; set; } }
public interface IFormWithWhatsApp { string? WhatsApp { get; set; } }
public interface IFormWithOrganization { string? OrganizationName { get; set; } }
