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

    public string NormalizePhone(string phone) =>
        phone.Trim()
            .Replace(" ", "", StringComparison.Ordinal)
            .Replace("-", "", StringComparison.Ordinal)
            .Replace("(", "", StringComparison.Ordinal)
            .Replace(")", "", StringComparison.Ordinal);

    public string HashEmail(string email) => ComputeHash(NormalizeEmail(email));

    public string HashPhone(string phone) => ComputeHash(NormalizePhone(phone));

    private string ComputeHash(string normalizedValue)
    {
        using var hmac = new HMACSHA256(_hashKey);
        return Convert.ToHexString(hmac.ComputeHash(Encoding.UTF8.GetBytes(normalizedValue))).ToLowerInvariant();
    }

    private static string? FirstConfigured(params string?[] values) =>
        values.FirstOrDefault(value => !string.IsNullOrWhiteSpace(value));
}
