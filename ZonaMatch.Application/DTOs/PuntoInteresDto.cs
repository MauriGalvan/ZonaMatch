namespace ZonaMatch.Application.DTOs
{
    // A point of interest classified into one of the app categories (CategoriaPuntoInteres).
    // Id is the stable OSM id: n<id> node, w<id> way, r<id> relation; or a<id> for a point added by neighbors.
    // Tipo is the raw OSM value that classified it (bus_stop, school, pharmacy...); Nombre may be missing (e.g. bus stops).
    // Fuente: "osm", or "vecinos" for a point added by neighbors (approved contributions)
    public record PuntoInteresDto(
        string Id,
        string? Nombre,
        string Categoria,
        string? Tipo,
        string? Operador,
        double Latitud,
        double Longitud,
        double DistanciaMetros,
        string Fuente);

    // Truncado = there were more points than the limit; only the closest ones are returned
    public record PuntosInteresCercanosDto(IReadOnlyList<PuntoInteresDto> Puntos, bool Truncado);

    // How many points of each category (and of each OSM type inside it) there are within a radius or a zone
    // RadioMetros is null when the area is a zone (GET /Zonas/{slug}/puntos-interes/resumen)
    public record ResumenPuntosInteresDto(double? RadioMetros, IReadOnlyList<ResumenCategoriaDto> Categorias);

    // Tipos ordered by Cantidad, descending. Every category is listed, with Total 0 when it has no points.
    public record ResumenCategoriaDto(string Categoria, int Total, IReadOnlyList<CantidadPorTipoDto> Tipos);

    public record CantidadPorTipoDto(string? Tipo, int Cantidad);

    // Raw row of the summary query
    public record CantidadPorTipo(string Categoria, string? Tipo, int Cantidad);
}
