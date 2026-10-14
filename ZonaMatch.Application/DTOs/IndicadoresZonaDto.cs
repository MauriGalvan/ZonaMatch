namespace ZonaMatch.Application.DTOs
{
    // GET /Zonas/{slug}/indicadores: datos abiertos de la zona. Los porcentajes tienen un decimal y son null
    // cuando no hay base para calcularlos. Cada bloque dice de donde sale y a que nivel geografico.
    public record IndicadoresZonaDto(
        string Slug,
        string Nombre,
        int NivelAdministrativo,
        PoblacionZonaDto Poblacion,
        ConectividadZonaDto Conectividad,
        CondicionesZonaDto Condiciones,
        // Null cuando la zona no tiene delitos con ubicacion (hoy, fuera de CABA)
        SeguridadZonaDto? Seguridad,
        IReadOnlyList<FuenteDatoDto> Fuentes);

    public record PoblacionZonaDto(
        int Total,
        double? DensidadHabitantesKm2,
        int Hogares,
        int Viviendas,
        double? PorcentajeMenores15,
        double? Porcentaje15a64,
        double? Porcentaje65yMas);

    // Referencia: el valor de CABA cuando la zona esta en la ciudad
    public record ConectividadZonaDto(
        double? PorcentajeHogaresInternet,
        double? PorcentajeHogaresComputadora,
        double? PorcentajeHogaresCelularInternet,
        ReferenciaDto? Referencia);

    public record CondicionesZonaDto(
        double? PorcentajeHogaresNbi,
        double? PorcentajeHogaresHacinamiento,
        double? PorcentajeHogaresAlquilan,
        double? PorcentajeCoberturaSalud,
        double? PorcentajeUniversitario);

    public record SeguridadZonaDto(
        int Anio,
        int Delitos,
        double? DelitosCada1000Habitantes,
        int Robos,
        int Hurtos,
        int Lesiones,
        int Homicidios,
        int Amenazas,
        int SiniestrosViales,
        ReferenciaDto? Referencia);

    // Valor del mismo indicador en un ambito mayor (p. ej. "CABA") para comparar
    public record ReferenciaDto(string Ambito, double? Valor);

    // Dato: "Poblacion", "Conectividad"...; Nivel: "Radio censal", "Barrio"...
    public record FuenteDatoDto(string Dato, string Fuente, string Nivel, int Anio);
}
