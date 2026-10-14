using ZonaMatch.Application.DTOs;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Perfil
{
    public static class PerfilMapeo
    {
        public static PerfilDto ADto(Contexto perfil) =>
            new(
                CriterioUsuario.ContextoADto(perfil),
                CriterioUsuario.CriteriosADto(perfil),
                CriterioUsuario.PuntosADto(perfil),
                perfil.FechaActualizacion);
    }
}
