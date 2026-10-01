using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using NetromEcoMeal.Constants;
using NetromEcoMeal.Services.Interfaces;

namespace NetromEcoMeal.Api.Controllers;

// Replaces the Blazor InputFile handlers in BusinessForm.razor/PackageForm.razor, which called
// IImageUploadService directly in-process. Over HTTP the file comes in as multipart form data instead.
[Route("api/uploads")]
[ApiController]
[Authorize]
public class UploadsController(IImageUploadService imageUploadService) : ControllerBase
{
    // subfolder is attacker-controlled input over HTTP (unlike the two hardcoded call sites
    // IImageUploadService.SaveAsync has today), and that service builds its save path from it
    // without sanitizing — so this allow-list is what actually stops a "../../" path-traversal
    // attempt, not the service itself.
    private static readonly HashSet<string> AllowedSubfolders = ["businesses", "packages"];

    [HttpPost("{subfolder}")]
    [RequestSizeLimit(ImageUpload.MaxSizeBytes)]
    public async Task<ActionResult<string>> Upload(string subfolder, IFormFile file)
    {
        if (!AllowedSubfolders.Contains(subfolder))
            return BadRequest("Unknown upload destination.");

        if (file.Length == 0)
            return BadRequest("No file was uploaded.");

        if (file.Length > ImageUpload.MaxSizeBytes)
            return BadRequest($"File is too large — the limit is {ImageUpload.MaxSizeBytes / (1024 * 1024)}MB.");

        await using var stream = file.OpenReadStream();
        var url = await imageUploadService.SaveAsync(stream, file.FileName, subfolder);
        return Ok(url);
    }
}
