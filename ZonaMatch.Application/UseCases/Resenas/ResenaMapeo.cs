using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.UseCases.Comunidad;
using ZonaMatch.Domain.Common;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Resenas
{
    // Resenas tal como las devuelve la API. "usuarioActual" es null en pedidos anonimos.
    internal static class ResenaMapeo
    {
        public static ResumenResenasDto Resumir(IReadOnlyList<Resena> resenas)
        {
            static double Promedio(IReadOnlyList<Resena> resenas, Func<Resena, int> puntaje) =>
                resenas.Count == 0 ? 0 : Math.Round(resenas.Average(puntaje), 1);

            return new ResumenResenasDto(
                Promedio(resenas, r => r.Puntaje),
                resenas.Count,
                resenas.Count(r => r.Verificada),
                [
                    new(AspectoZona.Seguridad, Promedio(resenas, r => r.PuntajeSeguridad)),
                    new(AspectoZona.Transporte, Promedio(resenas, r => r.PuntajeTransporte)),
                    new(AspectoZona.Conectividad, Promedio(resenas, r => r.PuntajeConectividad)),
                    new(AspectoZona.Comercios, Promedio(resenas, r => r.PuntajeComercios)),
                    new(AspectoZona.EspaciosVerdes, Promedio(resenas, r => r.PuntajeEspaciosVerdes)),
                ]);
        }

        public static ResenaDto ADto(Resena resena, string? emailAutor, Guid? usuarioActual) =>
            new(
                resena.Id,
                Autor.Iniciales(emailAutor),
                resena.AniosEnZona,
                resena.Verificada,
                resena.Puntaje,
                new PuntajesAspectosDto(
                    resena.PuntajeSeguridad,
                    resena.PuntajeTransporte,
                    resena.PuntajeConectividad,
                    resena.PuntajeComercios,
                    resena.PuntajeEspaciosVerdes),
                resena.Temas,
                resena.Texto,
                resena.FechaCreacion,
                resena.VotosUtil.Count,
                usuarioActual is not null && resena.VotosUtil.Any(v => v.UsuarioId == usuarioActual),
                resena.UsuarioId == usuarioActual);
    }
}
