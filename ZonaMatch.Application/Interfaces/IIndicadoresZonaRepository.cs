using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    // Lee los indicadores que calcula etl/calcular_zonas.sql. Null cuando la zona no existe o todavia no se
    // cargaron los datos (DATOS-ZONAS-DOCKER.bat).
    public interface IIndicadoresZonaRepository
    {
        // The same zone selection as GET /api/v1/zones/{slug} (the most specific match for duplicate names).
        Task<ZoneIndicators?> ObtenerPorSlugAsync(string slug, CancellationToken cancellationToken = default);

        // CABA entera, para comparar los barrios y las comunas
        Task<ZoneIndicators?> ObtenerCabaAsync(CancellationToken cancellationToken = default);
    }
}
