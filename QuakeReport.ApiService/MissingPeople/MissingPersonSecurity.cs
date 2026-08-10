using System.Security.Cryptography;
using System.Text;

namespace QuakeReport.ApiService.MissingPeople;

public sealed class MissingPersonSecurity(IConfiguration configuration)
{
    private string HmacKey => configuration["MissingPeople:IdHmacKey"]
        ?? throw new InvalidOperationException("MissingPeople:IdHmacKey is not configured.");

    public string Normalize(string value) => new(value.Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    public string HashIdentification(string value)
    {
        using var hmac = new HMACSHA256(Encoding.UTF8.GetBytes(HmacKey));
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(Normalize(value))));
    }

    public static string HashManagementCode(string value) =>
        Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(value)));

    public static string CreateManagementCode()
    {
        Span<byte> bytes = stackalloc byte[24];
        RandomNumberGenerator.Fill(bytes);
        return Convert.ToBase64String(bytes).Replace("+", "-").Replace("/", "_").TrimEnd('=');
    }

    public static bool Matches(string supplied, string expectedHash)
    {
        try
        {
            return CryptographicOperations.FixedTimeEquals(
                Convert.FromHexString(HashManagementCode(supplied)),
                Convert.FromHexString(expectedHash));
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
