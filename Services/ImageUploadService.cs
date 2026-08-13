using Netrom_Eco_Meal.Constants;
using Netrom_Eco_Meal.Services.Interfaces;

namespace Netrom_Eco_Meal.Services;

public class ImageUploadService(IWebHostEnvironment env) : IImageUploadService
{
    public async Task<string> SaveAsync(Stream content, string originalFileName, string subfolder)
    {
        var extension = Path.GetExtension(originalFileName).ToLowerInvariant();
        if (!ImageUpload.AllowedExtensions.Contains(extension))
            throw new InvalidOperationException($"\"{extension}\" isn't a supported image type — use JPG, PNG, WEBP, or GIF.");

        var uploadsDir = Path.Combine(env.WebRootPath, "uploads", subfolder);
        Directory.CreateDirectory(uploadsDir);

        // GUID filename — sidesteps both collisions and any path-traversal risk from the
        // original name, which is otherwise unused beyond its extension.
        var fileName = $"{Guid.NewGuid():N}{extension}";
        var filePath = Path.Combine(uploadsDir, fileName);

        await using (var fileStream = new FileStream(filePath, FileMode.Create))
        {
            await content.CopyToAsync(fileStream);
        }

        return $"/uploads/{subfolder}/{fileName}";
    }
}
