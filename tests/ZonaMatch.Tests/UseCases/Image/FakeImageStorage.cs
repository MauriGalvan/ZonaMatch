using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Tests.UseCases.Image;

internal sealed class FakeImageStorage : IImageStorage
{
    public List<(string Name, string ContentType)> Uploads { get; } = [];

    public Task UploadAsync(string name, Stream content, string contentType, CancellationToken cancellationToken = default)
    {
        Uploads.Add((name, contentType));
        return Task.CompletedTask;
    }

    public string GetUrl(string imageName) => $"https://images.test/{imageName}";
}
