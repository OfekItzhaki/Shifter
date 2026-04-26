namespace Jobuler.Application.Common;

/// <summary>
/// Validates uploaded image files by checking magic bytes (file signatures),
/// not just the Content-Type header or file extension.
/// Prevents disguised executables or malicious files from being stored.
/// </summary>
public static class ImageValidator
{
    public const long MaxFileSizeBytes = 10 * 1024 * 1024; // 10 MB

    private static readonly string[] AllowedContentTypes =
    [
        "image/jpeg",
        "image/png",
        "image/webp",
        "image/gif",
    ];

    // Magic byte signatures for each allowed format
    private static readonly (byte[] Signature, int Offset)[] Signatures =
    [
        // JPEG: FF D8 FF
        (new byte[] { 0xFF, 0xD8, 0xFF }, 0),
        // PNG: 89 50 4E 47 0D 0A 1A 0A
        (new byte[] { 0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A }, 0),
        // WebP: RIFF????WEBP (bytes 0-3 = RIFF, bytes 8-11 = WEBP)
        (new byte[] { 0x52, 0x49, 0x46, 0x46 }, 0), // RIFF — checked together with WEBP below
        // GIF87a / GIF89a
        (new byte[] { 0x47, 0x49, 0x46, 0x38, 0x37, 0x61 }, 0),
        (new byte[] { 0x47, 0x49, 0x46, 0x38, 0x39, 0x61 }, 0),
    ];

    public static void ValidateContentType(string contentType)
    {
        if (!AllowedContentTypes.Contains(contentType.ToLowerInvariant().Split(';')[0].Trim()))
            throw new InvalidOperationException(
                $"Unsupported image type '{contentType}'. Allowed: JPEG, PNG, WebP, GIF.");
    }

    public static void ValidateFileSize(long sizeBytes)
    {
        if (sizeBytes > MaxFileSizeBytes)
            throw new InvalidOperationException(
                $"File too large ({sizeBytes / 1024 / 1024} MB). Maximum allowed size is 10 MB.");
    }

    /// <summary>
    /// Reads the first 12 bytes of the stream and verifies they match a known image signature.
    /// Resets the stream position to 0 after reading.
    /// </summary>
    public static async Task ValidateMagicBytesAsync(Stream stream, CancellationToken ct = default)
    {
        var header = new byte[12];
        var read = await stream.ReadAsync(header.AsMemory(0, 12), ct);
        stream.Position = 0;

        if (read < 4)
            throw new InvalidOperationException("File is too small to be a valid image.");

        // Check standard signatures
        foreach (var (sig, offset) in Signatures)
        {
            if (read >= offset + sig.Length && header.Skip(offset).Take(sig.Length).SequenceEqual(sig))
            {
                // Extra check for WebP: bytes 8-11 must be "WEBP"
                if (sig[0] == 0x52) // RIFF
                {
                    if (read >= 12 && header[8] == 0x57 && header[9] == 0x45 && header[10] == 0x42 && header[11] == 0x50)
                        return; // Valid WebP
                    // RIFF but not WebP — reject
                    continue;
                }
                return; // Valid
            }
        }

        throw new InvalidOperationException(
            "File content does not match a valid image format. Upload JPEG, PNG, WebP, or GIF only.");
    }
}
