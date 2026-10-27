namespace ZonaMatch.Application.Emails;

// Builds links to the frontend for emails (Frontend:BaseUrl in the configuration).
public sealed class FrontendLinks(string baseUrl)
{
    public string BaseUrl { get; } = baseUrl.TrimEnd('/');

    // path starts with "/", for example "/zonas/palermo?tab=comunidad"
    public string To(string path) => $"{BaseUrl}{path}";
}
