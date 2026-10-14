using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Zonas
{
    // Poblacion, conectividad, condiciones de vida y seguridad de una zona, con sus fuentes
    public class ObtenerIndicadoresZona
    {
        // Anio de los datos cargados por etl/descargar_datos.py
        public const int AnioCenso = 2022;
        public const int AnioDelitos = 2024;

        private const string Caba = "CABA";

        private readonly IIndicadoresZonaRepository _indicadores;

        public ObtenerIndicadoresZona(IIndicadoresZonaRepository indicadores)
        {
            _indicadores = indicadores;
        }

        public async Task<IndicadoresZonaDto> EjecutarAsync(string slug, CancellationToken cancellationToken = default)
        {
            var zona = await _indicadores.ObtenerPorSlugAsync(slug, cancellationToken)
                ?? throw new NoEncontradoException("Todavia no hay datos de censo ni de seguridad para esta zona.");

            // Las zonas con delitos estan en CABA: se comparan con la ciudad entera
            var caba = zona.TieneDelitos ? await _indicadores.ObtenerCabaAsync(cancellationToken) : null;

            return new IndicadoresZonaDto(
                zona.Slug,
                zona.Nombre,
                zona.Nivel,
                new PoblacionZonaDto(
                    zona.Poblacion,
                    zona.DensidadHabitantesKm2,
                    zona.Hogares,
                    zona.Viviendas,
                    zona.PorcentajeMenores15,
                    zona.Porcentaje15a64,
                    zona.Porcentaje65yMas),
                new ConectividadZonaDto(
                    zona.PorcentajeHogaresInternet,
                    zona.PorcentajeHogaresComputadora,
                    zona.PorcentajeHogaresCelularInternet,
                    caba is null || EsCaba(zona) ? null : new ReferenciaDto(Caba, caba.PorcentajeHogaresInternet)),
                new CondicionesZonaDto(
                    zona.PorcentajeHogaresNbi,
                    zona.PorcentajeHogaresHacinamiento,
                    zona.PorcentajeHogaresAlquilan,
                    zona.PorcentajeCoberturaSalud,
                    zona.PorcentajeUniversitario),
                Seguridad(zona, caba),
                Fuentes(zona));
        }

        private static SeguridadZonaDto? Seguridad(IndicadoresZona zona, IndicadoresZona? caba)
        {
            if (zona.Delitos is not { } delitos)
                return null;

            return new SeguridadZonaDto(
                AnioDelitos,
                delitos,
                zona.DelitosCada1000Habitantes,
                zona.Robos ?? 0,
                zona.Hurtos ?? 0,
                zona.Lesiones ?? 0,
                zona.Homicidios ?? 0,
                zona.Amenazas ?? 0,
                zona.SiniestrosViales ?? 0,
                caba is null || EsCaba(zona) ? null : new ReferenciaDto(Caba, caba.DelitosCada1000Habitantes));
        }

        private static IReadOnlyList<FuenteDatoDto> Fuentes(IndicadoresZona zona)
        {
            var fuentes = new List<FuenteDatoDto>
            {
                new("Poblacion, hogares y conectividad", "Censo 2022 (INDEC)", "Radio censal", AnioCenso),
            };

            if (zona.TieneDelitos)
                fuentes.Add(new("Seguridad", "Mapa del Delito (Buenos Aires Data)", "Hecho georreferenciado", AnioDelitos));

            return fuentes;
        }

        private static bool EsCaba(IndicadoresZona zona) => zona.Nivel == NivelCaba;

        // admin_level de OSM de la Ciudad Autonoma de Buenos Aires
        private const int NivelCaba = 4;
    }
}
