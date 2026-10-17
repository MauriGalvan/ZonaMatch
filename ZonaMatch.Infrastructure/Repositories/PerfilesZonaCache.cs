using ZonaMatch.Domain.Puntaje;

namespace ZonaMatch.Infrastructure.Repositories
{
    // Perfiles de todas las zonas en memoria (singleton). Se calculan una vez y valen unas horas; si varios pedidos
    // llegan juntos con la cache vacia, se calcula una sola vez. Una lista vacia (faltan tablas) no se guarda.
    public sealed class PerfilesZonaCache
    {
        public static readonly TimeSpan Vigencia = TimeSpan.FromHours(6);

        private readonly SemaphoreSlim _calculando = new(1, 1);
        private readonly TimeProvider _reloj;

        private IReadOnlyList<PerfilZona>? _perfiles;
        private DateTimeOffset _vence;

        public PerfilesZonaCache(TimeProvider reloj)
        {
            _reloj = reloj;
        }

        public async Task<IReadOnlyList<PerfilZona>> ObtenerAsync(
            Func<CancellationToken, Task<IReadOnlyList<PerfilZona>>> calcular, CancellationToken cancellationToken)
        {
            if (Vigentes() is { } perfiles)
                return perfiles;

            await _calculando.WaitAsync(cancellationToken);
            try
            {
                // Otro pedido pudo haberlos calculado mientras se esperaba
                if (Vigentes() is { } calculados)
                    return calculados;

                var nuevos = await calcular(cancellationToken);
                if (nuevos.Count > 0)
                {
                    _perfiles = nuevos;
                    _vence = _reloj.GetUtcNow() + Vigencia;
                }

                return nuevos;
            }
            finally
            {
                _calculando.Release();
            }
        }

        private IReadOnlyList<PerfilZona>? Vigentes() =>
            _perfiles is not null && _reloj.GetUtcNow() < _vence ? _perfiles : null;
    }
}
