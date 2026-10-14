using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Application.UseCases.Resenas
{
    // Resumen de puntajes y resenas de una zona, la mas nueva primero. Con un usuario, cada resena indica si es suya.
    public class ListarResenas
    {
        private readonly IResenaRepository _resenas;

        public ListarResenas(IResenaRepository resenas)
        {
            _resenas = resenas;
        }

        public async Task<ResenasZonaDto> EjecutarAsync(string zonaSlug, Guid? usuarioActual, CancellationToken cancellationToken = default)
        {
            var resenas = await _resenas.ListarPorZonaAsync(zonaSlug, cancellationToken);

            return new ResenasZonaDto(
                ResenaMapeo.Resumir(resenas),
                resenas.Select(r => ResenaMapeo.ADto(r, r.Usuario?.Email, usuarioActual)).ToList());
        }
    }
}
