using Jobuler.Application.Common;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Jobuler.Infrastructure.Storage;

/// <summary>
/// Stores uploaded files on the local filesystem under {ContentRoot}/wwwroot/uploads.
/// Returns a public URL based on App:ApiBaseUrl config.
/// Swap for S3FileStorage in production by changing the DI registration.
/// </summary>
public class LocalDiskFileStorage : IFileStorage
{
    private readonly string _uploadRoot;
    private readonly string _baseUrl;
    private readonly ILogger<LocalDiskFileStorage> _logger;

    public LocalDiskFileStorage(
        IHostEnvironment env,
        IConfiguration configuration,
        ILogger<LocalDiskFileStorage> logger)
    {
        // Store files in wwwroot/uploads — served as static files by the API
        _uploadRoot = Path.Combine(env.ContentRootPath, "wwwroot", "uploads");
        Directory.CreateDirectory(_uploadRoot);

        var apiBase = configuration["App:ApiBaseUrl"]?.TrimEnd('/')
            ?? "http://localhost:5000";
        _baseUrl = $"{apiBase}/uploads";
        _logger = logger;
    }

    public async Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken ct = default)
    {
        // Generate a random filename to prevent path traversal and collisions
        var ext = Path.GetExtension(fileName).ToLowerInvariant();
        var safeExt = ext is ".jpg" or ".jpeg" or ".png" or ".webp" or ".gif" ? ext : ".bin";
        var storedName = $"{Guid.NewGuid():N}{safeExt}";
        var filePath = Path.Combine(_uploadRoot, storedName);

        await using var fs = new FileStream(filePath, FileMode.Create, FileAccess.Write, FileShare.None);
        await content.CopyToAsync(fs, ct);

        var url = $"{_baseUrl}/{storedName}";
        _logger.LogInformation("Saved upload: {FileName} → {Url}", fileName, url);
        return url;
    }

    public Task DeleteAsync(string publicUrl, CancellationToken ct = default)
    {
        try
        {
            var fileName = publicUrl.Split('/').Last();
            var filePath = Path.Combine(_uploadRoot, fileName);
            if (File.Exists(filePath))
            {
                File.Delete(filePath);
                _logger.LogInformation("Deleted upload: {FilePath}", filePath);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Failed to delete upload at {Url}", publicUrl);
        }
        return Task.CompletedTask;
    }
}
