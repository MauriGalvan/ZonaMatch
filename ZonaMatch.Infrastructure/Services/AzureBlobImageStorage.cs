using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Infrastructure.Services;

// Images live in an Azure Blob Storage container. publicRead must match the container access level
// (Terraform variable imagenes_publicas): public containers use the plain blob URL, private ones a signed link.
public sealed class AzureBlobImageStorage(BlobContainerClient container, bool publicRead) : IImageStorage
{
    private static readonly TimeSpan SignedLinkLifetime = TimeSpan.FromHours(1);

    public async Task UploadAsync(string name, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        var options = new BlobUploadOptions
        {
            // Names are unique and never overwritten, so browsers can cache the image for a long time.
            HttpHeaders = new BlobHttpHeaders { ContentType = contentType, CacheControl = "public, max-age=31536000, immutable" },
        };

        await container.GetBlobClient(name).UploadAsync(content, options, cancellationToken);
    }

    public string GetUrl(string imageName)
    {
        var blob = container.GetBlobClient(imageName);

        return publicRead
            ? blob.Uri.ToString()
            : blob.GenerateSasUri(BlobSasPermissions.Read, DateTimeOffset.UtcNow.Add(SignedLinkLifetime)).ToString();
    }
}
