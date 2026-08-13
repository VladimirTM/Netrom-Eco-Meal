using Microsoft.AspNetCore.Hosting;
using Moq;
using Netrom_Eco_Meal.Services;

namespace Netrom_Eco_Meal.Tests.Services;

public class ImageUploadServiceTests : IDisposable
{
    private readonly string _tempRoot = Path.Combine(Path.GetTempPath(), $"ecomeal-upload-tests-{Guid.NewGuid():N}");
    private readonly ImageUploadService _service;

    public ImageUploadServiceTests()
    {
        Directory.CreateDirectory(_tempRoot);
        var env = new Mock<IWebHostEnvironment>();
        env.Setup(e => e.WebRootPath).Returns(_tempRoot);
        _service = new ImageUploadService(env.Object);
    }

    [Fact]
    public async Task SaveAsync_ValidImage_WritesFileUnderSubfolderAndReturnsMatchingUrl()
    {
        await using var content = new MemoryStream([1, 2, 3, 4]);

        var url = await _service.SaveAsync(content, "photo.JPG", "packages");

        Assert.StartsWith("/uploads/packages/", url);
        Assert.EndsWith(".jpg", url);

        var savedPath = Path.Combine(_tempRoot, "uploads", "packages", Path.GetFileName(url));
        Assert.True(File.Exists(savedPath));
        Assert.Equal(new byte[] { 1, 2, 3, 4 }, await File.ReadAllBytesAsync(savedPath));
    }

    [Fact]
    public async Task SaveAsync_DifferentCalls_GetDistinctFileNames()
    {
        await using var contentA = new MemoryStream([1]);
        await using var contentB = new MemoryStream([2]);

        var urlA = await _service.SaveAsync(contentA, "a.png", "businesses");
        var urlB = await _service.SaveAsync(contentB, "b.png", "businesses");

        Assert.NotEqual(urlA, urlB);
    }

    [Fact]
    public async Task SaveAsync_DisallowedExtension_ThrowsAndWritesNothing()
    {
        await using var content = new MemoryStream([1, 2, 3]);

        await Assert.ThrowsAsync<InvalidOperationException>(() => _service.SaveAsync(content, "script.exe", "packages"));

        Assert.False(Directory.Exists(Path.Combine(_tempRoot, "uploads")));
    }

    public void Dispose()
    {
        if (Directory.Exists(_tempRoot))
            Directory.Delete(_tempRoot, recursive: true);
    }
}
