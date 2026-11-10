using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Image;

namespace ZonaMatch.Tests.UseCases.Image;

public class UploadImageTests
{
    private readonly FakeImageStorage _storage = new();
    private readonly UploadImage _useCase;

    public UploadImageTests()
    {
        _useCase = new UploadImage(_storage);
    }

    [Theory]
    [InlineData("image/jpeg", ".jpg")]
    [InlineData("image/png", ".png")]
    [InlineData("IMAGE/WEBP", ".webp")]
    public async Task Execute_StoresTheImageWithAGeneratedName(string contentType, string extension)
    {
        using var content = new MemoryStream([1, 2, 3]);

        var response = await _useCase.ExecuteAsync(content, contentType, content.Length);

        var upload = Assert.Single(_storage.Uploads);
        Assert.Equal(response.Name, upload.Name);
        Assert.EndsWith(extension, response.Name);
        Assert.Equal(contentType, upload.ContentType);
        Assert.Equal($"https://images.test/{response.Name}", response.Url);
    }

    [Theory]
    [InlineData("image/gif")]
    [InlineData("application/pdf")]
    [InlineData("")]
    public async Task Execute_WhenTheTypeIsNotAllowed_ThrowsBadRequest(string contentType)
    {
        using var content = new MemoryStream([1, 2, 3]);

        await Assert.ThrowsAsync<BadRequestException>(() => _useCase.ExecuteAsync(content, contentType, content.Length));

        Assert.Empty(_storage.Uploads);
    }

    [Theory]
    [InlineData("0123456789abcdef0123456789abcdef.jpg", true)]
    [InlineData("0123456789abcdef0123456789abcdef.webp", true)]
    [InlineData("0123456789abcdef0123456789abcdef.gif", false)]
    [InlineData("0123456789ABCDEF0123456789ABCDEF.jpg", false)]
    [InlineData("../0123456789abcdef0123456789abcdef.jpg", false)]
    [InlineData("photo.jpg", false)]
    [InlineData(null, false)]
    public void IsValidName_AcceptsOnlyGeneratedNames(string? name, bool expected)
    {
        Assert.Equal(expected, UploadImage.IsValidName(name));
    }

    [Fact]
    public async Task Execute_GeneratesNamesThatAreValid()
    {
        using var content = new MemoryStream([1]);

        var response = await _useCase.ExecuteAsync(content, "image/jpeg", content.Length);

        Assert.True(UploadImage.IsValidName(response.Name));
    }

    [Fact]
    public async Task Execute_WhenTheImageIsEmpty_ThrowsBadRequest()
    {
        using var content = new MemoryStream();

        await Assert.ThrowsAsync<BadRequestException>(() => _useCase.ExecuteAsync(content, "image/png", 0));

        Assert.Empty(_storage.Uploads);
    }

    [Fact]
    public async Task Execute_WhenTheImageIsTooLarge_ThrowsBadRequest()
    {
        using var content = new MemoryStream([1]);

        await Assert.ThrowsAsync<BadRequestException>(() =>
            _useCase.ExecuteAsync(content, "image/png", UploadImage.MaxSizeInBytes + 1));

        Assert.Empty(_storage.Uploads);
    }
}
