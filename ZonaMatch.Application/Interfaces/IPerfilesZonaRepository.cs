using ZonaMatch.Domain.Puntaje;

namespace ZonaMatch.Application.Interfaces
{
    // Datos de todas las zonas para el motor de puntaje (censo, delitos, transporte, equipamiento y alquiler)
    public interface IPerfilesZonaRepository
    {
        // Todas las zonas con datos; el motor compara cada una con las demas de su tipo
        Task<IReadOnlyList<PerfilZona>> ListarAsync(CancellationToken cancellationToken = default);

        // Zonas que limitan con alguna de las dadas, del mismo tipo (barrios y localidades, o comunas y partidos)
        Task<IReadOnlyList<string>> ListarVecinasAsync(IReadOnlyCollection<string> slugs, CancellationToken cancellationToken = default);
    }
}
