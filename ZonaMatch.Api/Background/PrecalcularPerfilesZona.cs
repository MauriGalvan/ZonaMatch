using System.Diagnostics;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Api.Background
{
    // Calcula los perfiles de zona al arrancar, para que el primer resultado de un analisis no espere unos segundos.
    // Si falla (por ejemplo, la base todavia no esta), el primer pedido los vuelve a intentar.
    public sealed class PrecalcularPerfilesZona : BackgroundService
    {
        private readonly IServiceScopeFactory _scopes;
        private readonly ILogger<PrecalcularPerfilesZona> _logger;

        public PrecalcularPerfilesZona(IServiceScopeFactory scopes, ILogger<PrecalcularPerfilesZona> logger)
        {
            _scopes = scopes;
            _logger = logger;
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            try
            {
                var reloj = Stopwatch.StartNew();
                using var scope = _scopes.CreateScope();
                var perfiles = await scope.ServiceProvider.GetRequiredService<IPerfilesZonaRepository>().ListarAsync(stoppingToken);
                _logger.LogInformation("Perfiles de zona para el puntaje: {Cantidad} zonas en {Segundos:F1} s.", perfiles.Count, reloj.Elapsed.TotalSeconds);
            }
            catch (Exception ex) when (!stoppingToken.IsCancellationRequested)
            {
                _logger.LogWarning(ex, "No se pudieron precalcular los perfiles de zona; se calculan con el primer pedido.");
            }
        }
    }
}
