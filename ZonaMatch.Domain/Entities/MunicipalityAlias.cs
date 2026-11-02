namespace ZonaMatch.Domain.Entities
{
    // Alternative municipality name mapped to a District (table geo.municipio_alias)
    public class MunicipalityAlias
    {
        public string MunicipalityName { get; set; } = string.Empty;
        public string DistrictKey { get; set; } = string.Empty;

        public District? District { get; set; }
    }
}
