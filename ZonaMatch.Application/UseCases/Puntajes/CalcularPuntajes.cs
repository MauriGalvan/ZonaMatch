using System.Text.RegularExpressions;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Puntaje;

namespace ZonaMatch.Application.UseCases.Puntajes
{
    // Ranks zones containing the user's places and their contiguous neighbors.
    public partial class CalcularPuntajes
    {
        public const int ZonasTopAutomatico = 5;

        // Maximum travel time when a place does not specify one.
        public const int TiempoMaximoPorDefecto = 45;

        public const int RecomendadasDevueltas = 4;

        // Number of routing requests sent concurrently.
        private const int ViajesEnParalelo = 6;

        private readonly IPerfilesZonaRepository _perfiles;
        private readonly IRuteoClient _ruteo;
        private readonly IZonaRepository _zonas;

        public CalcularPuntajes(IPerfilesZonaRepository perfiles, IRuteoClient ruteo, IZonaRepository zonas)
        {
            _perfiles = perfiles;
            _ruteo = ruteo;
            _zonas = zonas;
        }

        public async Task<PuntajesDto> EjecutarAsync(CalcularPuntajesRequest solicitud, CancellationToken cancellationToken = default)
        {
            var universo = await _perfiles.ListarAsync(cancellationToken);
            var porSlug = universo.ToDictionary(z => z.Slug);
            if (universo.Count == 0)
                throw new NotFoundException("Todavía no hay zonas disponibles para calcular el ranking.");

            var pedido = ArmarPedido(solicitud);
            var modo = ModosViaje.DesdeMovilidad(solicitud.Movilidad);
            var motor = new MotorPuntaje(universo);
            var zonasAgregadas = ResolverZonasAgregadas(solicitud.ZonasAgregadas, porSlug);
            var zonaInicialSlug = solicitud.ZonaInicial?.Trim();
            var zonaInicial = string.IsNullOrWhiteSpace(zonaInicialSlug)
                ? null
                : porSlug.TryGetValue(zonaInicialSlug, out var encontrada)
                    ? encontrada
                    : throw new NotFoundException("No hay datos para la zona inicial indicada.");

            var ubicaciones = pedido.Lugares
                .Where(lugar => lugar.TieneUbicacion)
                .Select(lugar => (Latitud: lugar.Latitud!.Value, Longitud: lugar.Longitud!.Value))
                .ToArray();
            var slugsDelEntorno = ubicaciones.Length == 0
                ? universo.Select(zona => zona.Slug).ToArray()
                : await _perfiles.ListarZonasDelEntornoAsync(ubicaciones, cancellationToken);
            var zonasDelEntorno = slugsDelEntorno
                .Where(porSlug.ContainsKey)
                .Select(slug => porSlug[slug])
                .ToArray();
            if (ubicaciones.Length > 0 && zonasDelEntorno.Length == 0)
                throw new BadRequestException("No se encontraron zonas del catálogo que contengan los puntos de interés indicados.");

            var candidatas = zonasDelEntorno
                .Concat(zonasAgregadas)
                .Concat(zonaInicial is { } inicial ? [inicial] : Array.Empty<PerfilZona>())
                .DistinctBy(zona => zona.Slug, StringComparer.Ordinal)
                .ToArray();

            var origenes = await ResolverOrigenesAsync(solicitud.OrigenesZona, porSlug, cancellationToken);
            var tiempos = await MedirViajesAsync(candidatas, pedido.Lugares, modo, origenes, cancellationToken);
            var ranking = candidatas
                .Select(zona => motor.Calcular(zona, pedido, tiempos[zona.Slug]))
                .OrderByDescending(resultado => resultado.Puntaje.HasValue)
                .ThenByDescending(resultado => resultado.Puntaje)
                .ThenBy(resultado => resultado.Zona.Nombre, StringComparer.CurrentCulture)
                .ToArray();

            var top = ranking.Take(ZonasTopAutomatico).ToList();
            if (zonaInicial is not null && top.All(resultado => resultado.Zona.Slug != zonaInicial.Slug))
            {
                if (top.Count == ZonasTopAutomatico)
                    top.RemoveAt(top.Count - 1);
                top.Add(ranking.Single(resultado => resultado.Zona.Slug == zonaInicial.Slug));
                top = top
                    .OrderByDescending(resultado => resultado.Puntaje.HasValue)
                    .ThenByDescending(resultado => resultado.Puntaje)
                    .ThenBy(resultado => resultado.Zona.Nombre, StringComparer.CurrentCulture)
                    .ToList();
            }

            var incluidas = top
                .Concat(zonasAgregadas.Select(zona => ranking.Single(resultado => resultado.Zona.Slug == zona.Slug)))
                .DistinctBy(resultado => resultado.Zona.Slug, StringComparer.Ordinal)
                .OrderByDescending(resultado => resultado.Puntaje.HasValue)
                .ThenByDescending(resultado => resultado.Puntaje)
                .ThenBy(resultado => resultado.Zona.Nombre, StringComparer.CurrentCulture)
                .ToArray();
            var slugsIncluidos = incluidas.Select(resultado => resultado.Zona.Slug).ToHashSet(StringComparer.Ordinal);
            var recomendadas = ranking
                .Where(resultado => !slugsIncluidos.Contains(resultado.Zona.Slug))
                .Take(RecomendadasDevueltas)
                .ToArray();

            return new PuntajesDto(
                ModosViaje.Codigo(modo),
                incluidas.Select(ADto).ToArray(),
                recomendadas.Select(ADto).ToArray());
        }

        private static PerfilZona[] ResolverZonasAgregadas(
            IReadOnlyCollection<string>? slugsSolicitados,
            IReadOnlyDictionary<string, PerfilZona> porSlug)
        {
            var slugs = (slugsSolicitados ?? [])
                .Select(slug => slug?.Trim() ?? string.Empty)
                .Where(slug => slug.Length > 0)
                .Distinct(StringComparer.Ordinal)
                .ToArray();
            var faltantes = slugs.Where(slug => !porSlug.ContainsKey(slug)).ToArray();
            if (faltantes.Length > 0)
                throw new NotFoundException("No se encontraron una o más zonas agregadas al análisis.");

            return slugs.Select(slug => porSlug[slug]).ToArray();
        }

        // Starting points chosen by the user, by zone slug. Each one must fall inside its zone.
        private async Task<Dictionary<string, (double Latitud, double Longitud)>> ResolverOrigenesAsync(
            IReadOnlyCollection<OrigenZonaRequest>? origenes, IReadOnlyDictionary<string, PerfilZona> porSlug,
            CancellationToken cancellationToken)
        {
            var resultado = new Dictionary<string, (double Latitud, double Longitud)>(StringComparer.Ordinal);
            foreach (var origen in origenes ?? [])
            {
                var slug = origen.Slug.Trim();
                // A starting point for a zone that does not exist cannot be measured
                if (!porSlug.ContainsKey(slug))
                    throw new NotFoundException("No se encontró la zona del punto de partida indicado.");

                // The point replaces the center of the zone, so it has to be inside it
                if (!await _zonas.ContienePuntoAsync(slug, origen.Latitude, origen.Longitude, cancellationToken))
                    throw new BadRequestException("El punto de partida tiene que estar dentro de su zona.");

                resultado[slug] = (origen.Latitude, origen.Longitude);
            }

            return resultado;
        }

        // One trip per zone and located place, starting at the zone center or at the point the user chose in it.
        // Estimate the time if routing is unavailable.
        private async Task<Dictionary<string, IReadOnlyList<TiempoViaje?>>> MedirViajesAsync(
            IReadOnlyList<PerfilZona> zonas, IReadOnlyList<LugarDelUsuario> lugares, ModoViaje modo,
            IReadOnlyDictionary<string, (double Latitud, double Longitud)> origenes,
            CancellationToken cancellationToken)
        {
            using var limite = new SemaphoreSlim(ViajesEnParalelo);

            async Task<TiempoViaje?> MedirAsync(PerfilZona zona, LugarDelUsuario lugar)
            {
                if (!lugar.TieneUbicacion)
                    return null;

                var (latitud, longitud) = origenes.TryGetValue(zona.Slug, out var origen) ? origen : (zona.Latitud, zona.Longitud);

                await limite.WaitAsync(cancellationToken);
                try
                {
                    // Ten blocks or less are walked (ModosViaje.DistanciaACaminarKm)
                    var modoDelViaje = ModosViaje.ParaDistancia(modo, latitud, longitud, lugar.Latitud!.Value, lugar.Longitud!.Value);
                    return await _ruteo.CalcularAsync(latitud, longitud, lugar.Latitud!.Value, lugar.Longitud!.Value, modoDelViaje, cancellationToken)
                           ?? ModosViaje.Estimar(modoDelViaje, latitud, longitud, lugar.Latitud!.Value, lugar.Longitud!.Value);
                }
                finally
                {
                    limite.Release();
                }
            }

            var porZona = zonas
                .DistinctBy(z => z.Slug)
                .Select(async zona => (zona.Slug, Tiempos: await Task.WhenAll(lugares.Select(l => MedirAsync(zona, l)))))
                .ToList();

            var resultados = await Task.WhenAll(porZona);
            return resultados.ToDictionary(r => r.Slug, r => (IReadOnlyList<TiempoViaje?>)r.Tiempos);
        }

        private static PedidoPuntaje ArmarPedido(CalcularPuntajesRequest solicitud)
        {
            var criterios = solicitud.Criterios
                .Where(c => MotorPuntaje.EsCriterioConocido(c.Key) && c.Value is "baja" or "media" or "alta")
                .ToDictionary(c => c.Key, c => c.Value!);

            var lugares = solicitud.Lugares
                .Select(l => new LugarDelUsuario(l.Nombre.Trim(), l.Latitude, l.Longitude, l.Prioridad, Minutos(l.TiempoMax)))
                .ToList();

            return new PedidoPuntaje(criterios, lugares, Pesos(solicitud.AlquilerMaximo));
        }

        // "45 min" -> 45.
        private static int Minutos(string? tiempo) =>
            tiempo is not null && Numero().Match(tiempo) is { Success: true } m && int.TryParse(m.Value, out var minutos) && minutos > 0
                ? minutos
                : TiempoMaximoPorDefecto;

        // "$ 800.000" or "800000" -> 800000; null when no number is present.
        private static decimal? Pesos(string? monto)
        {
            var digitos = new string((monto ?? string.Empty).Where(char.IsDigit).ToArray());
            return decimal.TryParse(digitos, out var valor) && valor > 0 ? valor : null;
        }

        [GeneratedRegex(@"\d+")]
        private static partial Regex Numero();

        private static PuntajeZonaDto ADto(ResultadoZona resultado) =>
            new(
                resultado.Zona.Slug,
                resultado.Zona.Nombre,
                resultado.Puntaje,
                resultado.Componentes.Select(c => new ComponentePuntajeDto(c.Criterio, c.Prioridad, c.Puntaje, c.Detalle)).ToList(),
                resultado.Limites.Select(l => new LimitePuntajeDto(l.Tipo, l.Cumple, l.Detalle)).ToList(),
                resultado.Lugares.Select(l => new TiempoLugarDto(
                    l.Nombre, l.Minutos, l.MaximoMinutos, l.Estimado, l.DistanciaKm)).ToList(),
                resultado.Zona.AlquilerPromedio is { } promedio
                    ? new AlquilerZonaDto(promedio, resultado.Zona.PropiedadesDisponibles ?? 0)
                    : null);
    }
}
