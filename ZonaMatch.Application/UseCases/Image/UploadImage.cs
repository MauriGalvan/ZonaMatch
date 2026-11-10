using System.Text.RegularExpressions;
using ZonaMatch.Application.DTOs.Image.Responses;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Image;

public sealed class UploadImage(IImageStorage imageStorage)
{
    public const long MaxSizeInBytes = 5 * 1024 * 1024;

    private static readonly Dictionary<string, string> Extensions = new(StringComparer.OrdinalIgnoreCase)
    {
        ["image/jpeg"] = ".jpg",
        ["image/png"] = ".png",
        ["image/webp"] = ".webp",
    };

    // True for names this use case generates (32 hex characters plus an allowed extension).
    public static bool IsValidName(string? name) =>
        name is not null && Regex.IsMatch(name, @"^[0-9a-f]{32}\.(jpg|png|webp)$");

    public async Task<ImageResponse> ExecuteAsync(
        Stream content,
        string contentType,
        long length,
        CancellationToken cancellationToken = default)
    {
        if (length <= 0)
            throw new BadRequestException("La imagen está vacía.");

        if (length > MaxSizeInBytes)
            throw new BadRequestException("La imagen no puede superar los 5 MB.");

        if (!Extensions.TryGetValue(contentType, out var extension))
            throw new BadRequestException("Solo se admiten imágenes JPEG, PNG o WebP.");

        // A generated name avoids collisions and never reuses what the client sent.
        var name = $"{Guid.NewGuid():N}{extension}";
        await imageStorage.UploadAsync(name, content, contentType, cancellationToken);

        return new ImageResponse(name, imageStorage.GetUrl(name));
    }
}
