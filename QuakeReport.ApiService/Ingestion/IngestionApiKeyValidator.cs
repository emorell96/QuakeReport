using System.Security.Cryptography;
using System.Text;

namespace QuakeReport.ApiService.Ingestion;

public interface IIngestionApiKeyValidator
{
    bool IsValid(string? supplied);
    string Fingerprint(string supplied);
}

public sealed class IngestionApiKeyValidator(IConfiguration configuration) : IIngestionApiKeyValidator
{
    public bool IsValid(string? supplied)
    {
        if (string.IsNullOrWhiteSpace(supplied)) return false;
        var expected = configuration["Ingestion:ApiKey"];
        if (string.IsNullOrWhiteSpace(expected)) return false;
        return CryptographicOperations.FixedTimeEquals(Hash(supplied), Hash(expected));
    }

    public string Fingerprint(string supplied) => Convert.ToHexString(Hash(supplied));

    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}
