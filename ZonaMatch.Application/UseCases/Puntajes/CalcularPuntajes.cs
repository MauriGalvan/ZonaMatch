using System.Text.RegularExpressions;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Puntaje;

namespace ZonaMatch.Application.UseCases.Puntajes
{
    // Puntaje de las zonas de un analisis y de las zonas vecinas que mejor le cierran al usuario.
    // Las vecinas se ordenan primero sin viajes (es rapido) y solo a las mejores se les miden los viajes.
    public partial class CalcularPuntajes
    {
        // Tiempo maximo de viaje si el lugar no lo indica
        public const int TiempoMaximoPorDefecto = 45;

        // Vecinas a las que se les miden los viajes, y cuantas se devuelven
        public const int VecinasCandidatas = 6;
        public const int VecinasDevueltas = 4;

        // Viajes que se piden al ruteo a la vez
        private const int ViajesEnParalelo = 6;

        private readonly IPerfilesZonaRepository _perfiles;
        private readonly IRuteoClient _ruteo;

        public CalcularPuntajes(IPerfilesZonaRepository perfiles, IRuteoClient ruteo)
        {
            _perfiles = perfiles;
            _ruteo = ruteo;
        }

        public async Task<PuntajesDto> EjecutarAsync(CalcularPuntajesRequest solicitud, CancellationToken cancellationToken = default)
        {
            var universo = await _perfiles.ListarAsync(cancellationToken);
            var porSlug = universo.ToDictionary(z => z.Slug);

            var slugs = solicitud.Zonas.Select(s => s.Trim()).Where(s => s.Length > 0).Distinct().ToList();
            var analizadas = slugs.Where(porSlug.ContainsKey).Select(s => porSlug[s]).ToList();

            if (analizadas.Count == 0)
                throw new NoEncontradoException("Todavía no hay datos para calcular el puntaje de estas zonas.");

            var pedido = ArmarPedido(solicitud);
            var modo = ModosViaje.DesdeMovilidad(solicitud.Movilidad);
            var motor = new MotorPuntaje(universo);

            var candidatas = solicitud.IncluirCercanas
                ? await CandidatasAsync(analizadas, porSlug, motor, pedido, cancellationToken)
                : [];

            var tiempos = await MedirViajesAsync([.. analizadas, .. candidatas], pedido.Lugares, modo, cancellationToken);

            var zonas = analizadas.Select(z => motor.Calcular(z, pedido, tiempos[z.Slug])).ToList();
            var cercanas = candidatas
                .Select(z => motor.Calcular(z, pedido, tiempos[z.Slug]))
                .Where(r => r.Puntaje is not null)
                .OrderByDescending(r => r.Puntaje)
                .Take(VecinasDevueltas)
                .ToList();

            return new PuntajesDto(ModosViaje.Codigo(modo), zonas.Select(ADto).ToList(), cercanas.Select(ADto).ToList());
        }

        // Las vecinas con mejor puntaje sin contar los viajes (todavia no se midieron)
        private async Task<List<PerfilZona>> CandidatasAsync(
            IReadOnlyList<PerfilZona> analizadas, IReadOnlyDictionary<string, PerfilZona> porSlug, MotorPuntaje motor,
            PedidoPuntaje pedido, CancellationToken cancellationToken)
        {
            var slugsAnalizados = analizadas.Select(z => z.Slug).ToHashSet();
            var vecinas = await _perfiles.ListarVecinasAsync(slugsAnalizados, cancellationToken);

            return vecinas
                .Where(s => !slugsAnalizados.Contains(s) && porSlug.ContainsKey(s))
                .Select(s => porSlug[s])
                .Select(z => (Zona: z, Puntaje: motor.Calcular(z, pedido).Puntaje))
                .Where(x => x.Puntaje is not null)
                .OrderByDescending(x => x.Puntaje)
                .Take(VecinasCandidatas)
                .Select(x => x.Zona)
                .ToList();
        }

        // Un viaje por zona y lugar con ubicacion, desde el centro de la zona. Si el ruteo no responde, se estima.
        private async Task<Dictionary<string, IReadOnlyList<TiempoViaje?>>> MedirViajesAsync(
            IReadOnlyList<PerfilZona> zonas, IReadOnlyList<LugarDelUsuario> lugares, ModoViaje modo,
            CancellationToken cancellationToken)
        {
            using var limite = new SemaphoreSlim(ViajesEnParalelo);

            async Task<TiempoViaje?> MedirAsync(PerfilZona zona, LugarDelUsuario lugar)
            {
                if (!lugar.TieneUbicacion)
                    return null;

                await limite.WaitAsync(cancellationToken);
                try
                {
                    return await _ruteo.CalcularAsync(zona.Latitud, zona.Longitud, lugar.Latitud!.Value, lugar.Longitud!.Value, modo, cancellationToken)
                           ?? ModosViaje.Estimar(modo, zona.Latitud, zona.Longitud, lugar.Latitud!.Value, lugar.Longitud!.Value);
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

        // "45 min" -> 45
        private static int Minutos(string? tiempo) =>
            tiempo is not null && Numero().Match(tiempo) is { Success: true } m && int.TryParse(m.Value, out var minutos) && minutos > 0
                ? minutos
                : TiempoMaximoPorDefecto;

        // "$ 800.000" o "800000" -> 800000; null si no hay numero
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
                resultado.Lugares.Select(l => new TiempoLugarDto(l.Nombre, l.Minutos, l.MaximoMinutos, l.Estimado)).ToList(),
                resultado.Zona.AlquilerPromedio is { } promedio
                    ? new AlquilerZonaDto(promedio, resultado.Zona.PropiedadesDisponibles ?? 0)
                    : null);
    }
}
