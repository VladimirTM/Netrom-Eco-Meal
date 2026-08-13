namespace Netrom_Eco_Meal.Constants;

// Shared by ImageUploadService and every form's InputFile handler (client-side size check
// before the stream is even opened, so an oversized file fails fast with a friendly message
// instead of a mid-upload IOException).
public static class ImageUpload
{
    public const long MaxSizeBytes = 5 * 1024 * 1024;
    public static readonly string[] AllowedExtensions = [".jpg", ".jpeg", ".png", ".webp", ".gif"];
}
