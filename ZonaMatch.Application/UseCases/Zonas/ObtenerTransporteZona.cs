using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Common;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Zonas
{
    // Public transport stops, routes, and frequencies by time window for a zone.
    public class ObtenerTransporteZona
    {
        // Display order for transport modes.
        private static readonly string[] Modos = ["subte", "tren", "colectivo"];

        private readonly ITransporteZonaRepository _transporte;
        private readonly IZonaRepository _zonas;

        public ObtenerTransporteZona(ITransporteZonaRepository transporte, IZonaRepository zonas)
        {
            _transporte = transporte;
            _zonas = zonas;
        }

        public async Task<TransporteZonaDto> EjecutarAsync(string slug, CancellationToken cancellationToken = default)
        {
            var paradas = await _transporte.ListarParadasAsync(slug, cancellationToken);

            // No stops may mean the zone has no transit service or does not exist.
            if (paradas.Count == 0 && !await _zonas.ExistsAsync(slug, cancellationToken))
                throw new NotFoundException("No se encontro una zona con ese identificador.");

            var salidas = paradas.Count == 0 ? [] : await _transporte.ListarSalidasAsync(slug, cancellationToken);

            var modos = paradas
                .OrderBy(p => Array.IndexOf(Modos, p.Modo) is var orden and >= 0 ? orden : Modos.Length)
                .Select(p => Modo(p, salidas.Where(s => s.Modo == p.Modo).ToList()))
                .ToList();

            return new TransporteZonaDto(slug, modos, Fuentes(modos));
        }

        private static ModoTransporteDto Modo(ParadasModoZona paradas, IReadOnlyList<SalidasLineaZona> salidas)
        {
            var franjas = FranjaHoraria.Todas
                .Select(franja =>
                {
                    var enFranja = salidas.Where(s => s.Franja == franja.Codigo && s.Salidas > 0).ToList();
                    return new FranjaTransporteDto(
                        franja.Codigo, franja.Desde, franja.Hasta,
                        enFranja.Select(s => s.Linea).Distinct().Count(),
                        franja.SalidasPorHora(enFranja.Sum(s => s.Salidas)));
                })
                .ToList();

            var lineas = salidas
                .GroupBy(s => s.Linea)
                .Select(linea => new
                {
                    Linea = linea.Key,
                    Paradas = linea.Max(s => s.Paradas),
                    Pico = linea.Where(s => s.Franja == FranjaHoraria.Manana.Codigo).Sum(s => s.Salidas),
                    Franjas = FranjaHoraria.Todas
                        .Select(franja =>
                        {
                            var cantidad = linea.Where(s => s.Franja == franja.Codigo).Sum(s => s.Salidas);
                            return new FrecuenciaLineaDto(franja.Codigo, franja.SalidasPorHora(cantidad), franja.MinutosEntreSalidas(cantidad));
                        })
                        .ToList(),
                })
                .OrderByDescending(l => l.Pico)
                .ThenBy(l => l.Linea, StringComparer.Ordinal)
                .Select(l => new LineaTransporteDto(l.Linea, l.Paradas, l.Franjas))
                .ToList();

            return new ModoTransporteDto(paradas.Modo, paradas.Paradas, paradas.Lineas, franjas, lineas);
        }

        private static IReadOnlyList<FuenteDatoDto> Fuentes(IReadOnlyList<ModoTransporteDto> modos)
        {
            var anios = new Dictionary<string, (string Fuente, int Anio)>
            {
                ["colectivo"] = ("GTFS de colectivos (Buenos Aires Data)", 2019),
                ["tren"] = ("GTFS de trenes (Buenos Aires Data)", 2020),
                ["subte"] = ("GTFS de subte (Buenos Aires Data)", 2019),
            };

            return modos
                .Where(m => anios.ContainsKey(m.Modo))
                .Select(m => new FuenteDatoDto("Transporte", anios[m.Modo].Fuente, "Parada", anios[m.Modo].Anio))
                .ToList();
        }
    }
}
