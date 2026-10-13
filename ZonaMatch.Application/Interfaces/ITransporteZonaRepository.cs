using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    // Lee el resumen de transporte que calcula etl/calcular_transporte.sql. Listas vacias cuando la zona no
    // tiene paradas o todavia no se cargaron los datos (DATOS-ZONAS-DOCKER.bat).
    public interface ITransporteZonaRepository
    {
        // La misma zona que GET /Zonas/{slug}
        Task<IReadOnlyList<ParadasModoZona>> ListarParadasAsync(string slug, CancellationToken cancellationToken = default);

        Task<IReadOnlyList<SalidasLineaZona>> ListarSalidasAsync(string slug, CancellationToken cancellationToken = default);
    }
}
