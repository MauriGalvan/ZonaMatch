using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.Interfaces;
using ZonaMatch.Domain.Common;
using ZonaMatch.Domain.Entities;

namespace ZonaMatch.Application.Services
{
    public class ResenaService : IResenaService
    {
        private readonly IResenaRepository _resenas;
        private readonly IZonaRepository _zonas;
        private readonly TimeProvider _timeProvider;

        public ResenaService(IResenaRepository resenas, IZonaRepository zonas, TimeProvider timeProvider)
        {
            _resenas = resenas;
            _zonas = zonas;
            _timeProvider = timeProvider;
        }

        public async Task<ResenasZonaDto> ListarAsync(string zonaSlug, Guid? usuarioActual, CancellationToken cancellationToken = default)
        {
            var resenas = await _resenas.ListarPorZonaAsync(zonaSlug, cancellationToken);

            return new ResenasZonaDto(
                Resumir(resenas),
                resenas.Select(r => ADto(r, r.Usuario?.Email, usuarioActual)).ToList());
        }

        public async Task<ResenaDto> GuardarMiaAsync(
            string zonaSlug, SesionDto usuario, GuardarResenaDto request, CancellationToken cancellationToken = default)
        {
            await ValidarZonaAsync(zonaSlug, cancellationToken);

            if (!AspectoZona.TryNormalizar(request.Temas, out var temas, out var error))
                throw new DatosInvalidosException(error);

            var ahora = _timeProvider.GetUtcNow();
            var resena = await _resenas.ObtenerDeUsuarioAsync(zonaSlug, usuario.Id, cancellationToken);

            if (resena is null)
            {
                resena = new Resena { ZonaSlug = zonaSlug, UsuarioId = usuario.Id, FechaCreacion = ahora };
                _resenas.Agregar(resena);
            }

            var aspectos = request.Aspectos!; // guaranteed by [Required] on the DTO
            resena.Puntaje = request.Puntaje;
            resena.PuntajeSeguridad = aspectos.Seguridad;
            resena.PuntajeTransporte = aspectos.Transporte;
            resena.PuntajeConectividad = aspectos.Conectividad;
            resena.PuntajeComercios = aspectos.Comercios;
            resena.PuntajeEspaciosVerdes = aspectos.EspaciosVerdes;
            resena.Texto = request.Texto.Trim();
            resena.AniosEnZona = request.AniosEnZona;
            resena.Temas = temas.ToList();
            resena.FechaActualizacion = ahora;

            await _resenas.GuardarCambiosAsync(cancellationToken);

            return ADto(resena, usuario.Email, usuario.Id);
        }

        public async Task EliminarMiaAsync(string zonaSlug, Guid usuarioId, CancellationToken cancellationToken = default)
        {
            var resena = await _resenas.ObtenerDeUsuarioAsync(zonaSlug, usuarioId, cancellationToken)
                ?? throw new NoEncontradoException("No escribiste una resena de esta zona.");

            _resenas.Eliminar(resena);
            await _resenas.GuardarCambiosAsync(cancellationToken);
        }

        public async Task MarcarUtilAsync(
            string zonaSlug, Guid resenaId, Guid usuarioId, bool util, CancellationToken cancellationToken = default)
        {
            var resena = await _resenas.ObtenerAsync(zonaSlug, resenaId, cancellationToken)
                ?? throw new NoEncontradoException("La resena no existe.");

            if (resena.UsuarioId == usuarioId)
                throw new OperacionNoPermitidaException("No podes marcar como util tu propia resena.");

            var voto = resena.VotosUtil.FirstOrDefault(v => v.UsuarioId == usuarioId);

            // Idempotent: marking twice or unmarking a review that was not marked changes nothing
            if (util && voto is null)
                resena.VotosUtil.Add(new ResenaVotoUtil { ResenaId = resena.Id, UsuarioId = usuarioId, Fecha = _timeProvider.GetUtcNow() });
            else if (!util && voto is not null)
                resena.VotosUtil.Remove(voto);
            else
                return;

            await _resenas.GuardarCambiosAsync(cancellationToken);
        }

        internal static ResumenResenasDto Resumir(IReadOnlyList<Resena> resenas)
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

        private static ResenaDto ADto(Resena resena, string? emailAutor, Guid? usuarioActual) =>
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

        private async Task ValidarZonaAsync(string zonaSlug, CancellationToken cancellationToken)
        {
            if (!await _zonas.ExisteAsync(zonaSlug, cancellationToken))
                throw new NoEncontradoException("No se encontro una zona con ese identificador.");
        }
    }
}
