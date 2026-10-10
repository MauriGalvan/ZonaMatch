using ZonaMatch.Application.DTOs;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.PuntosInteres
{
    internal static class PuntoInteresUsuarioMapeo
    {
        // Requiere el Tag cargado
        public static PuntoInteresUsuarioDto ADto(PuntoInteresUsuario punto) =>
            new(
                punto.Id,
                punto.Tag.Nombre,
                punto.Alias,
                punto.Direccion,
                punto.Barrio,
                new CoordenadaDto(punto.Ubicacion.Y, punto.Ubicacion.X),
                punto.FechaCreacion);
    }
}
