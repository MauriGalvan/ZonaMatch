using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Application.Models;
using ZonaMatch.Application.UseCases.Comunidad;
using ZonaMatch.Domain.Common;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.UseCases.Resenas
{
    // Creates or replaces the user's review for a zone (one per user and zone).
    public class GuardarMiResena
    {
        private readonly IResenaRepository _resenas;
        private readonly IZonaRepository _zonas;
        private readonly TimeProvider _reloj;

        public GuardarMiResena(IResenaRepository resenas, IZonaRepository zonas, TimeProvider reloj)
        {
            _resenas = resenas;
            _zonas = zonas;
            _reloj = reloj;
        }

        public async Task<ResenaDto> EjecutarAsync(
            string zonaSlug, AuthenticatedUserContext currentUser, GuardarResenaDto solicitud, CancellationToken cancellationToken = default)
        {
            await _zonas.ValidarExisteAsync(zonaSlug, cancellationToken);

            if (!AspectoZona.TryNormalizar(solicitud.Temas, out var temas, out var error))
                throw new BadRequestException(error);

            var ahora = _reloj.GetUtcNow();
            var resena = await _resenas.ObtenerDeUsuarioAsync(zonaSlug, currentUser.Id, cancellationToken);

            if (resena is null)
            {
                resena = new Resena { ZonaSlug = zonaSlug, UsuarioId = currentUser.Id, FechaCreacion = ahora };
                _resenas.Agregar(resena);
            }

            var aspectos = solicitud.Aspectos!; // Required by DTO validation.
            resena.PuntajeSeguridad = aspectos.Seguridad;
            resena.PuntajeTransporte = aspectos.Transporte;
            resena.PuntajeConectividad = aspectos.Conectividad;
            resena.PuntajeComercios = aspectos.Comercios;
            resena.PuntajeEspaciosVerdes = aspectos.EspaciosVerdes;
            resena.Texto = solicitud.Texto.Trim();
            resena.AniosEnZona = solicitud.AniosEnZona;
            resena.Temas = temas.ToList();
            resena.FechaActualizacion = ahora;

            await _resenas.GuardarCambiosAsync(cancellationToken);

            return ResenaMapeo.ADto(resena, currentUser.Email, currentUser.Id);
        }
    }
}
