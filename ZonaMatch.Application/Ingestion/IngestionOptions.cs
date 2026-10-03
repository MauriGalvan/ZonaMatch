using ZonaMatch.Domain.Territory;

namespace ZonaMatch.Application.Ingestion
{
    // describe cómo leer cada fuente; cambiar de fuente es cambiar configuración, no código
    public class IngestionOptions
    {
        public const string SectionName = "Ingestion";

        public List<TerritorialSourceOptions> TerritorialSources { get; set; } = [];
        public List<PoiSourceOptions> PoiSources { get; set; } = [];

        // códigos INDEC de los 24 partidos del conurbano: solo sus localidades son zonas
        public List<string> ScopePartidoCodes { get; set; } = [];

        public double DuplicateMaxDistanceMeters { get; set; } = 100d;
        public int BatchSize { get; set; } = 500;
    }

    public class TerritorialSourceOptions
    {
        public string DataSourceCode { get; set; } = string.Empty;
        public TerritorialUnitType Type { get; set; }
        public string Table { get; set; } = string.Empty;
        public string IdColumn { get; set; } = string.Empty;
        public string NameColumn { get; set; } = string.Empty;
        public string GeometryColumn { get; set; } = string.Empty;
        public string? CodeColumn { get; set; }

        // con ParentCodeColumn el padre se busca por código; sin ella, por contención espacial
        public TerritorialUnitType? ParentType { get; set; }
        public string? ParentCodeColumn { get; set; }

        // condición SQL adicional definida por quien opera la ingesta (no por usuarios)
        public string? Filter { get; set; }

        // cuando varias filas comparten id (partido + sus islas), las que cumplen esta condición
        // aportan el nombre y el código; si no se define, aporta la de mayor superficie
        public string? PreferredRowCondition { get; set; }

        public DateOnly? SourceDate { get; set; }
    }

    public class PoiSourceOptions
    {
        public string DataSourceCode { get; set; } = string.Empty;
        public string Table { get; set; } = string.Empty;
        public string IdColumn { get; set; } = string.Empty;
        public string NameColumn { get; set; } = string.Empty;
        public string GeometryColumn { get; set; } = string.Empty;

        // prefijo del id externo; con OsmIdentifiers los ids negativos son relaciones ("r")
        public string IdPrefix { get; set; } = string.Empty;
        public bool OsmIdentifiers { get; set; }

        public List<string> AddressColumns { get; set; } = [];
        public List<string> AttributeColumns { get; set; } = [];
        // columna hstore con todas las etiquetas (osm2pgsql --hstore)
        public string? TagsColumn { get; set; }

        public string? OwnershipAttribute { get; set; }
        public List<string> PublicValues { get; set; } = [];
        public List<string> PrivateValues { get; set; } = [];

        public string? Filter { get; set; }
        public DateOnly? SourceDate { get; set; }
    }
}
