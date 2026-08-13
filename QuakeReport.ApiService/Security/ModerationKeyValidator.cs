using System.Security.Cryptography;
using System.Text;

namespace QuakeReport.ApiService.Security;

public interface IModerationKeyValidator
{
    /// <summary>Checks a supplied X-Moderation-Service-Key header against the configured moderator key.</summary>
    bool IsValid(string? supplied);
}

/// <summary>
/// Hashes both sides before comparing, then compares fixed-length digests with
/// a constant-time comparison. Comparing the raw supplied/expected bytes directly
/// (as several controllers used to) is vulnerable to a timing attack: FixedTimeEquals
/// still short-circuits on a length mismatch, which leaks the expected key's length
/// and gives an attacker a signal per guessed length. Hashing first normalizes both
/// inputs to the same length before the constant-time comparison runs.
/// </summary>
public sealed class ModerationKeyValidator(IConfiguration configuration) : IModerationKeyValidator
{
    public bool IsValid(string? supplied)
    {
        if (string.IsNullOrWhiteSpace(supplied)) return false;
        var expected = configuration["Moderation:ApiKey"];
        if (string.IsNullOrWhiteSpace(expected)) return false;
        return CryptographicOperations.FixedTimeEquals(Hash(supplied), Hash(expected));
    }

    private static byte[] Hash(string value) => SHA256.HashData(Encoding.UTF8.GetBytes(value));
}
