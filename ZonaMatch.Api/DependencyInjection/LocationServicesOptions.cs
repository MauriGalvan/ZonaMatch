namespace ZonaMatch.Api.DependencyInjection;

internal sealed class LocationServicesOptions
{
    public ProviderSelection Geocoding { get; set; } = new();
    public ProviderSelection PlaceSearch { get; set; } = new();
    public HttpSettings Nominatim { get; set; } = new();
    public HttpSettings Photon { get; set; } = new();

    public bool IsEnabled(string provider) => Geocoding.CanSelect(provider) || PlaceSearch.CanSelect(provider);

    public static bool IsHttpBaseUrl(string? value) =>
        Uri.TryCreate(value, UriKind.Absolute, out var uri)
        && uri.Scheme is "http" or "https"
        && uri.Query.Length == 0
        && uri.Fragment.Length == 0;

    public static Uri ToBaseAddress(string value) => new(value.Trim().TrimEnd('/') + '/');

    public sealed class ProviderSelection
    {
        public string Provider { get; set; } = string.Empty;
        public bool AllowProviderOverride { get; set; }

        private string? ProviderName => Provider?.Trim().ToLowerInvariant();
        public bool IsValidSearchProvider => ProviderName is "nominatim" or "photon";
        public bool IsValidGeocodingProvider => IsValidSearchProvider || ProviderName is "osm";
        public bool CanSelect(string provider) => AllowProviderOverride || ProviderName == provider;
    }

    public sealed class HttpSettings
    {
        public string? BaseUrl { get; set; }
        public TimeSpan Timeout { get; set; }
        public string? Language { get; set; }
        public string? UserAgent { get; set; }
    }
}
