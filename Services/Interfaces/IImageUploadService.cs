namespace Netrom_Eco_Meal.Services.Interfaces;

// Local-disk storage under wwwroot/uploads/{subfolder} — see BACKEND_ARCHITECTURE.md §5 and
// Program.cs's dedicated UseStaticFiles middleware for /uploads (MapStaticAssets alone only
// serves the build-time asset manifest, not files written here at runtime).
public interface IImageUploadService
{
    // originalFileName is only used for its extension. Throws InvalidOperationException for an
    // extension outside Constants.ImageUpload.AllowedExtensions — callers show that message
    // directly, same convention PackageTypeService's delete-in-use guard uses. Returns the
    // public, web-relative URL (e.g. "/uploads/packages/{guid}.jpg") to store as ImageUrl.
    Task<string> SaveAsync(Stream content, string originalFileName, string subfolder);
}
