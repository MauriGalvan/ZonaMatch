namespace ZonaMatch.Application.Interfaces;

public interface IImageStorage
{
    // Saves the image under the given name.
    Task UploadAsync(string name, Stream content, string contentType, CancellationToken cancellationToken = default);

    // URL to display a stored image. When the storage is private it is a temporary signed link,
    // so persist the image name and call this when the image is read.
    string GetUrl(string imageName);
}
