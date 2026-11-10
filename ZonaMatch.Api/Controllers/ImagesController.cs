using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ZonaMatch.Application.DTOs.Image.Responses;
using ZonaMatch.Application.UseCases.Image;

namespace ZonaMatch.Api.Controllers;

// Uploads an image to Azure Blob Storage. The caller keeps the returned name to attach the image to its content.
[ApiController]
[Route("api/v1/images")]
public sealed class ImagesController : ControllerBase
{
    // POST /api/v1/images Form data: file=<JPEG, PNG or WebP up to 5 MB>
    // Responds with { "name": "<guid>.jpg", "url": "https://..." }
    [Authorize]
    [HttpPost]
    [EnableRateLimiting("image-upload")]
    [RequestSizeLimit(6 * 1024 * 1024)]
    [Consumes("multipart/form-data")]
    [ProducesResponseType<ImageResponse>(StatusCodes.Status201Created)]
    [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status401Unauthorized)]
    [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
    public async Task<IActionResult> UploadAsync(
        IFormFile file,
        [FromServices] UploadImage useCase,
        CancellationToken cancellationToken)
    {
        await using var content = file.OpenReadStream();
        var image = await useCase.ExecuteAsync(content, file.ContentType, file.Length, cancellationToken);
        return StatusCode(StatusCodes.Status201Created, image);
    }
}
