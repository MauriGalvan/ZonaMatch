namespace ZonaMatch.Domain.Entities
{
    // Alternative municipality name mapped to a District (table geo.municipio_alias)
    public class MunicipalityAlias
    {
        public string MunicipioNombre { get; set; } = string.Empty;
        public string PartidoKey { get; set; } = string.Empty;

        public District? District { get; set; }
    }
}
