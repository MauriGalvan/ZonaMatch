using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.UseCases.Comunidad;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Preguntas
{
    internal static class RespuestaMapeo
    {
        public static RespuestaDto ADto(Respuesta respuesta, string? emailAutor, Guid? usuarioActual) =>
            new(respuesta.Id, respuesta.Texto, Autor.Iniciales(emailAutor), respuesta.FechaCreacion, respuesta.UsuarioId == usuarioActual);
    }
}
