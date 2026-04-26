namespace Jobuler.Application.Common;

/// <summary>
/// Abstraction for storing uploaded files.
/// Swap implementations via DI: LocalDiskFileStorage for dev, S3FileStorage for prod.
/// </summary>
public interface IFileStorage
{
    /// <summary>
    /// Saves a file and returns the public URL to access it.
    /// </summary>
    Task<string> SaveAsync(Stream content, string fileName, string contentType, CancellationToken ct = default);

    /// <summary>
    /// Deletes a file by its public URL. No-op if the file doesn't exist.
    /// </summary>
    Task DeleteAsync(string publicUrl, CancellationToken ct = default);
}
