using System.Security.Cryptography;
using System.Text;
using Jobuler.Application.Common;
using Microsoft.Extensions.Configuration;

namespace Jobuler.Infrastructure.Security;

public sealed class ContactLookupProtector : IContactLookupProtector
{
    private readonly byte[] _hashKey;

    public ContactLookupProtector(IConfiguration configuration)
    {
        var secret = FirstConfigured(
                configuration["DataProtection:FieldHashKey"],
                Environment.GetEnvironmentVariable("FIELD_HASH_KEY"),
                configuration["DataProtection:FieldEncryptionKey"],
                Environment.GetEnvironmentVariable("FIELD_ENCRYPTION_KEY"),
                configuration["Jwt:Secret"])
            ?? throw new InvalidOperationException("No contact lookup hash key is configured.");

        _hashKey = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }

    public string NormalizeEmail(string email) => email.Trim().ToLowerInvariant();

    public string NormalizePhone(string phone)
    {
        var compact = phone.Trim()
            .Replace(" ", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal)
            .Replace("(", "", StringComparison.Ordinal)
            .Replace(")", "", StringComparison.Ordinal)
            .Replace(".", "", StringComparison.Ordinal);

        if (compact.StartsWith("00", StringComparison.Ordinal))
            compact = $"+{compact[2..]}";

        var digits = compact.StartsWith('+') ? compact[1..] : compact;
        if (!digits.All(char.IsDigit))
            return compact;

        if (digits.StartsWith("972", StringComparison.Ordinal))
            return $"+{digits}";

        if (digits.StartsWith('0') && IsLikelyIsraeliNationalNumber(digits))
            return $"+972{digits[1..]}";

        if (IsLikelyIsraeliSubscriberNumber(digits))
            return $"+972{digits}";

        return compact;
    }

    public string HashEmail(string email) => ComputeHash(NormalizeEmail(email));

    public string HashPhone(string phone) => ComputeHash(NormalizePhone(phone));

    private string ComputeHash(string normalizedValue)
    {
        using var hmac = new HMACSHA256(_hashKey);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(normalizedValue))).ToLowerInvariant();
    }

    private static string? FirstConfigured(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));

    private static bool IsLikelyIsraeliNationalNumber(string digits)
    {
        if (digits.Length is not (9 or 10)) return false;
        return digits[0] == '0' && IsLikelyIsraeliSubscriberNumber(digits[1..]);
    }

    private static bool IsLikelyIsraeliSubscriberNumber(string digits)
    {
        if (digits.Length == 9)
            return digits[0] is '5' or '7' && digits.All(char.IsDigit);

        if (digits.Length == 8)
            return digits[0] is '2' or '3' or '4' or '8' or '9' && digits.All(char.IsDigit);

        return false;
    }
}
