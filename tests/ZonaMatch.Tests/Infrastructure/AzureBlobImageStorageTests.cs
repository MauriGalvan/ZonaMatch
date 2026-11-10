using Azure.Storage.Blobs;
using ZonaMatch.Infrastructure.Services;

namespace ZonaMatch.Tests.Infrastructure;

public class AzureBlobImageStorageTests
{
    // Fake account key: enough to build the client and sign links without reaching Azure.
    private static readonly string ConnectionString =
        $"DefaultEndpointsProtocol=https;AccountName=testaccount;AccountKey={Convert.ToBase64String(new byte[32])};EndpointSuffix=core.windows.net";

    [Fact]
    public void GetUrl_WhenTheContainerIsPublic_ReturnsTheBlobUrl()
    {
        var storage = new AzureBlobImageStorage(new BlobContainerClient(ConnectionString, "imagenes"), publicRead: true);

        Assert.Equal("https://testaccount.blob.core.windows.net/imagenes/photo.jpg", storage.GetUrl("photo.jpg"));
    }

    [Fact]
    public void GetUrl_WhenTheContainerIsPrivate_ReturnsAReadOnlySignedUrl()
    {
        var storage = new AzureBlobImageStorage(new BlobContainerClient(ConnectionString, "imagenes"), publicRead: false);

        var url = storage.GetUrl("photo.jpg");

        Assert.StartsWith("https://testaccount.blob.core.windows.net/imagenes/photo.jpg?", url);
        Assert.Contains("sp=r", url);
        Assert.Contains("sig=", url);
    }
}
