using Jobuler.Application.Common;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Jobuler.Api.Controllers;

/// <summary>
/// Handles image uploads for profile photos, group images, space logos, etc.
/// Returns a public URL that can be stored on any entity's image field.
/// </summary>
[ApiController]
[Route("uploads")]
[Authorize]
public class UploadsController : ControllerBase
{
    private readonly IFileStorage _storage;

    public UploadsController(IFileStorage storage) => _storage = storage;

    /// <summary>
    /// Upload an image file. Returns the public URL.
    /// Accepts: JPEG, PNG, WebP, GIF. Max size: 10 MB.
    /// </summary>
    [HttpPost("image")]
    [RequestSizeLimit(10 * 1024 * 1024 + 4096)] // 10 MB + multipart overhead
    public async Task<IActionResult> UploadImage(IFormFile file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
            return BadRequest(new { message = "No file provided." });

        try
        {
            // 1. Validate content-type header
            ImageValidator.ValidateContentType(file.ContentType);

            // 2. Validate file size
            ImageValidator.ValidateFileSize(file.Length);

            // 3. Validate magic bytes (actual file content)
            await using var stream = file.OpenReadStream();
            await ImageValidator.ValidateMagicBytesAsync(stream, ct);

            // 4. Save and return URL
            var url = await _storage.SaveAsync(stream, file.FileName, file.ContentType, ct);
            return Ok(new { url });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { message = ex.Message });
        }
    }
}
