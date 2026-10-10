using NetTopologySuite.Geometries;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Comunidad;
using ZonaMatch.Domain.Common;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Aportes
{
    // Aportes desde los pedidos y de vuelta hacia la API. "usuarioActual" es null en pedidos anonimos.
    internal static class AporteMapeo
    {
        public static string ValidarCategoria(string? categoria)
        {
            var codigo = categoria?.Trim().ToLowerInvariant();
            if (codigo is null || !CategoriaPuntoInteres.Todas.Contains(codigo))
                throw new DatosInvalidosException(
                    $"Categoria desconocida. Validas: {string.Join(", ", CategoriaPuntoInteres.Todas)}.");

            return codigo;
        }

        public static Point Punto(double latitud, double longitud) => new(longitud, latitud) { SRID = 4326 };

        public static string? Limpiar(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();

        public static AporteDto ADto(Aporte aporte, string? emailAutor, Guid? usuarioActual) =>
            new(
                aporte.Id,
                aporte.Tipo,
                aporte.Estado,
                Autor.Iniciales(emailAutor),
                aporte.FechaCreacion,
                aporte.UsuarioId == usuarioActual,
                aporte.Validaciones.FirstOrDefault(v => v.UsuarioId == usuarioActual)?.Confirma,
                aporte.Confirmaciones,
                aporte.Rechazos,
                aporte.Nombre,
                aporte.Categoria,
                aporte.Ubicacion is null ? null : new CoordenadaDto(aporte.Ubicacion.Y, aporte.Ubicacion.X),
                aporte.Horario,
                aporte.Direccion,
                aporte.ViveOTrabajaEnZona,
                aporte.PuntoInteresId,
                aporte.PuntoInteresNombre,
                aporte.Motivo,
                aporte.Comentario);
    }
}
