using Amazon;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Jobuler.Application.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;

namespace Jobuler.Infrastructure.Storage;

/// <summary>
/// Stores uploaded files in any S3-compatible object storage (AWS S3, MinIO, Cloudflare R2, etc.).
///
/// Required configuration:
///   Storage:S3:BucketName     — the bucket to store files in (must exist)
///   Storage:S3:Region         — AWS region (e.g. "us-east-1") or "us-east-1" for MinIO
///   Storage:S3:AccessKey      — access key ID
///   Storage:S3:SecretKey      — secret access key
///   Storage:S3:ServiceUrl     — (optional) custom endpoint for MinIO/R2, e.g. "http://minio:9000"
///   Storage:S3:PublicBaseUrl  — (optional) public CDN/proxy URL prefix, e.g. "https://cdn.example.com/uploads"
///                               If not set, falls back to the S3 presigned URL pattern.
///   Storage:S3:ForcePathStyle — (optional) "true" for MinIO (required), "false" for AWS (default)
///
/// Files are stored with public-read ACL so they can be served directly.
/// For private buckets, use presigned URLs instead (swap GetPublicUrl for GeneratePresignedUrl).
/// </summary>
public class S3FileStorage : IFileStorage
{
    private readonly IAmazonS3 _s3;
    private readonly string _bucket;
    private readonly string? _publicBaseUrl;
    private readonly ILogger<S3FileStorage> _logger;

    public S3FileStorage(IConfiguration config, ILogger<S3FileStorage> logger)
    {
        _logger = logger;

        var section = config.GetSection("Storage:S3");
        _bucket = section["BucketName"]
            ?? throw new InvalidOperationException("Storage:S3:BucketName is required.");

        var accessKey = section["AccessKey"]
            ?? throw new InvalidOperationException("Storage:S3:AccessKey is required.");
        var secretKey = section["SecretKey"]
            ?? throw new InvalidOperationException("Storage:S3:SecretKey is required.");
        var region = section["Region"] ?? "us-east-1";
        var serviceUrl = section["ServiceUrl"];
        var forcePathStyle = section["ForcePathStyle"]?.Equals("true", StringComparison.OrdinalIgnoreCase) ?? false;

        _publicBaseUrl = section["PublicBaseUrl"]?.TrimEnd('/');

        var credentials = new BasicAWSCredentials(accessKey, secretKey);
        var s3Config = new AmazonS3Config
        {
            RegionEndpoint = RegionEndpoint.GetBySystemName(region),
            ForcePathStyle = forcePathStyle,
        };

        if (!string.IsNullOrWhiteSpace(serviceUrl))
        {
            s3Config.ServiceURL = serviceUrl;
            s3Config.ForcePathStyle = true; // always required for custom endpoints
        }

        _s3 = new AmazonS3Client(credentials, s3Config);
    }

    public async Task<string> SaveAsync(
        Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        // Generate a random key to prevent collisions and path traversal
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var safeExt = ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" ? ext : ".bin";
        var key = $"uploads/{Guid.NewGuid():N}{safeExt}";

        var request = new PutObjectRequest
        {
            BucketName = _bucket,
            Key = key,
            InputStream = content,
            ContentType = contentType,
            CannedACL = S3CannedACL.PublicRead,
            AutoCloseStream = false,
        };

        await _s3.PutObjectAsync(request, ct);

        var url = _publicBaseUrl is not null
            ? $"{_publicBaseUrl}/{key}"
            : $"https://{_bucket}.s3.amazonaws.com/{key}";

        _logger.LogInformation("Saved upload to S3: {Key} → {Url}", key, url);
        return url;
    }

    public async Task DeleteAsync(string publicUrl, CancellationToken ct = default)
    {
        try
        {
            // Extract the key from the URL
            string key;
            if (_publicBaseUrl is not null && publicUrl.StartsWith(_publicBaseUrl))
            {
                key = publicUrl[((_publicBaseUrl.Length + 1))..]; // strip base URL + leading slash
            }
            else
            {
                // Fall back: take everything after the bucket name in the URL
                var uri = new Uri(publicUrl);
                key = uri.AbsolutePath.TrimStart('/');
                // For path-style URLs: /bucket/key → strip bucket prefix
                if (key.StartsWith(_bucket + "/"))
                    key = key[(_bucket.Length + 1)..];
            }

            await _s3.DeleteObjectAsync(_bucket, key, ct);
            _logger.LogInformation("Deleted S3 object: {Key}", key);
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete S3 object at {Url}", publicUrl);
        }
    }
}
