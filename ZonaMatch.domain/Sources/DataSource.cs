using ZonaMatch.Domain.Common;

namespace ZonaMatch.Domain.Sources
{
    public enum DataSourceKind : short
    {
        // organismo público (INDEC, GCBA, PBA)
        Official = 1,
        // datos colaborativos (OpenStreetMap, vecinos)
        Community = 2,
    }

    // procedencia de cada dato del modelo canónico (trazabilidad, PBI 37)
    public class DataSource
    {
        public DataSource(string code, string name, DataSourceKind kind, string? publisher, string? url, string? license)
        {
            Code = Guard.NotBlank(code, nameof(code));
            Name = Guard.NotBlank(name, nameof(name));
            Kind = kind;
            Publisher = publisher;
            Url = url;
            License = license;
        }

        public short Id { get; private set; }
        public string Code { get; private set; }
        public string Name { get; private set; }
        public DataSourceKind Kind { get; private set; }
        public string? Publisher { get; private set; }
        public string? Url { get; private set; }
        public string? License { get; private set; }
    }
}
