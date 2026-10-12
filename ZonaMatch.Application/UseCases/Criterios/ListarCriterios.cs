using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Criterios
{
    // Catalogo compartido por el wizard detallado y el de invitado: solo activos, por orden.
    public class ListarCriterios
    {
        private readonly ICriterioRepository _criterios;

        public ListarCriterios(ICriterioRepository criterios)
        {
            _criterios = criterios;
        }

        public async Task<IReadOnlyList<CriterioDto>> EjecutarAsync(CancellationToken cancellationToken = default)
        {
            var criterios = await _criterios.ListarAsync(cancellationToken);

            return criterios
                .Where(c => c.Activo)
                .Select(c => new CriterioDto(c.Id, c.Nombre, c.Descripcion, c.Icono, c.Orden))
                .ToList();
        }
    }
}
