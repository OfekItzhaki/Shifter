using Jobuler.Infrastructure.Storage;
using Microsoft.AspNetCore.Hosting;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.FileProviders;
using Microsoft.Extensions.Logging.Abstractions;
using Xunit;

namespace Jobuler.Tests.Application;

public class LocalDiskFileStorageTests
{
    [Fact]
    public async Task SaveAsync_UsesApiBaseUrl_WhenLocalPublicBaseUrlIsEmpty()
    {
        var webRoot = CreateTempDirectory();
        try
        {
            var storage = CreateStorage(webRoot, new Dictionary<string, string?>
            {
                ["App:ApiBaseUrl"] = "https://api.shifter.ofeklabs.com"
            });

            var url = await storage.SaveAsync(
                new MemoryStream(TestPngBytes()),
                "avatar.png",
                "image/png");

            Assert.StartsWith("https://api.shifter.ofeklabs.com/uploads/", url);
            Assert.EndsWith(".png", url);
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    [Fact]
    public async Task SaveAsync_UsesLocalPublicBaseUrl_WhenConfigured()
    {
        var webRoot = CreateTempDirectory();
        try
        {
            var storage = CreateStorage(webRoot, new Dictionary<string, string?>
            {
                ["App:ApiBaseUrl"] = "https://api.shifter.ofeklabs.com",
                ["Storage:Local:PublicBaseUrl"] = "https://uploads.shifter.ofeklabs.com/"
            });

            var url = await storage.SaveAsync(
                new MemoryStream(TestPngBytes()),
                "avatar.png",
                "image/png");

            Assert.StartsWith("https://uploads.shifter.ofeklabs.com/uploads/", url);
            Assert.EndsWith(".png", url);
        }
        finally
        {
            Directory.Delete(webRoot, recursive: true);
        }
    }

    private static LocalDiskFileStorage CreateStorage(string webRoot, Dictionary<string, string?> values)
    {
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(values)
            .Build();

        return new LocalDiskFileStorage(
            new TestWebHostEnvironment(webRoot),
            configuration,
            NullLogger<LocalDiskFileStorage>.Instance);
    }

    private static byte[] TestPngBytes() =>
        [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A, 0x00, 0x00, 0x00, 0x00];

    private static string CreateTempDirectory()
    {
        var path = Path.Combine(Path.GetTempPath(), $"shifter-storage-test-{Guid.NewGuid():N}");
        Directory.CreateDirectory(path);
        return path;
    }

    private sealed class TestWebHostEnvironment(string webRootPath) : IWebHostEnvironment
    {
        public string ApplicationName { get; set; } = "Jobuler.Tests";
        public IFileProvider ContentRootFileProvider { get; set; } = new NullFileProvider();
        public string ContentRootPath { get; set; } = webRootPath;
        public string EnvironmentName { get; set; } = "Testing";
        public IFileProvider WebRootFileProvider { get; set; } = new NullFileProvider();
        public string WebRootPath { get; set; } = webRootPath;
    }
}
