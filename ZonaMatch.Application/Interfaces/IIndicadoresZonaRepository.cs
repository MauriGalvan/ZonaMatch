using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Interfaces
{
    // Lee los indicadores que calcula etl/calcular_zonas.sql. Null cuando la zona no existe o todavia no se
    // cargaron los datos (DATOS-ZONAS-DOCKER.bat).
    public interface IIndicadoresZonaRepository
    {
        // La misma zona que GET /Zonas/{slug} (ante nombres repetidos, la mas especifica)
        Task<IndicadoresZona?> ObtenerPorSlugAsync(string slug, CancellationToken cancellationToken = default);

        // CABA entera, para comparar los barrios y las comunas
        Task<IndicadoresZona?> ObtenerCabaAsync(CancellationToken cancellationToken = default);
    }
}
