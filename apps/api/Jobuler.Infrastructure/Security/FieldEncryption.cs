using System.Security.Cryptography;
using System.Text;

namespace Jobuler.Infrastructure.Security;

public static class FieldEncryption
{
    private const string Prefix = "enc:v1:";
    private static byte[]? _key;

    public static void Configure(string secret)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(secret);
        _key = SHA256.HashData(Encoding.UTF8.GetBytes(secret));
    }

    public static string Encrypt(string value)
    {
        if (string.IsNullOrEmpty(value) || value.StartsWith(Prefix, StringComparison.Ordinal))
            return value;

        var key = GetKey();
        var nonce = RandomNumberGenerator.GetBytes(12);
        var plaintext = Encoding.UTF8.GetBytes(value);
        var ciphertext = new byte[plaintext.Length];
        var tag = new byte[16];

        using var aes = new AesGcm(key, tag.Length);
        aes.Encrypt(nonce, plaintext, ciphertext, tag);

        var payload = new byte[nonce.Length + tag.Length + ciphertext.Length];
        Buffer.BlockCopy(nonce, 0, payload, 0, nonce.Length);
        Buffer.BlockCopy(tag, 0, payload, nonce.Length, tag.Length);
        Buffer.BlockCopy(ciphertext, 0, payload, nonce.Length + tag.Length, ciphertext.Length);

        return Prefix + Convert.ToBase64String(payload);
    }

    public static string? EncryptNullable(string? value) =>
        string.IsNullOrEmpty(value) ? value : Encrypt(value);

    public static string Decrypt(string value)
    {
        if (string.IsNullOrEmpty(value) || !value.StartsWith(Prefix, StringComparison.Ordinal))
            return value;

        var payload = Convert.FromBase64String(value[Prefix.Length..]);
        if (payload.Length < 29)
            throw new CryptographicException("Encrypted field payload is invalid.");

        var nonce = payload[..12];
        var tag = payload[12..28];
        var ciphertext = payload[28..];
        var plaintext = new byte[ciphertext.Length];

        using var aes = new AesGcm(GetKey(), tag.Length);
        aes.Decrypt(nonce, ciphertext, tag, plaintext);

        return Encoding.UTF8.GetString(plaintext);
    }

    public static string? DecryptNullable(string? value) =>
        string.IsNullOrEmpty(value) ? value : Decrypt(value);

    private static byte[] GetKey()
    {
        if (_key is null)
            throw new InvalidOperationException("Field encryption is not configured.");

        return _key;
    }
}
