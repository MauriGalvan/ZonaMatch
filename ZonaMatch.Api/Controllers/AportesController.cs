using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.UseCases.Aportes;

namespace ZonaMatch.Api.Controllers
{
    // Community contributions to a zone map: new points and corrections stay pending until other users
    // validate them. Approved contributions appear in the points of interest endpoints.
    [ApiController]
    [Route("Zonas/{slug:regex(" + RutasZona.Slug + ")}/aportes")]
    public class AportesController : ControllerBase
    {
        // GET /Zonas/villa-luro/aportes/resumen
        [HttpGet("resumen")]
        [ProducesResponseType<ResumenAportesDto>(StatusCodes.Status200OK)]
        public async Task<IActionResult> Resumir(
            string slug,
            [FromServices] ResumirAportes resumirAportes,
            CancellationToken cancellationToken) =>
            Ok(await resumirAportes.EjecutarAsync(slug, cancellationToken));

        // GET /Zonas/villa-luro/aportes/pendientes
        // Oldest first. With a token, each item indicates whether it is yours (esMio) and your vote (miVoto).
        [HttpGet("pendientes")]
        [ProducesResponseType<IReadOnlyList<AporteDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> ListarPendientes(
            string slug,
            [FromServices] ListarAportesPendientes listarAportesPendientes,
            CancellationToken cancellationToken) =>
            Ok(await listarAportesPendientes.EjecutarAsync(slug, User.GetAuthenticatedUserId(), cancellationToken));

        // POST /Zonas/villa-luro/aportes/puntos
        // Body: { "categoria": "salud", "nombre": "", "horario": "", "direccion": "", "latitud": -34.63,
        //         "longitud": -58.50, "viveOTrabajaEnZona": true }
        [Authorize]
        [HttpPost("puntos")]
        [ProducesResponseType<AporteDto>(StatusCodes.Status201Created)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CrearPunto(
            string slug,
            [FromBody] CrearPuntoNuevoDto solicitud,
            [FromServices] CrearPuntoNuevo crearPuntoNuevo,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserContext() is not { } currentUser)
                return Unauthorized();

            var aporte = await crearPuntoNuevo.EjecutarAsync(slug, currentUser, solicitud, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, aporte);
        }

        // POST /Zonas/villa-luro/aportes/correcciones
        // Body: { "puntoInteresId": "n123", "puntoInteresNombre": "", "motivo": "cerro|nombre|ubicacion|categoria|otro",
        //         "nombrePropuesto": "", "categoriaPropuesta": "", "latitud": 0, "longitud": 0, "comentario": "" }
        [Authorize]
        [HttpPost("correcciones")]
        [ProducesResponseType<AporteDto>(StatusCodes.Status201Created)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CrearCorreccion(
            string slug,
            [FromBody] CrearCorreccionDto solicitud,
            [FromServices] CrearCorreccion crearCorreccion,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserContext() is not { } currentUser)
                return Unauthorized();

            var aporte = await crearCorreccion.EjecutarAsync(slug, currentUser, solicitud, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, aporte);
        }

        // PUT /Zonas/villa-luro/aportes/{id}/validacion    Body: { "confirma": true }
        // Cast or change your vote on another user's pending contribution.
        [Authorize]
        [HttpPut("{id:guid}/validacion")]
        [ProducesResponseType<AporteDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Validar(
            string slug,
            Guid id,
            [FromBody] ValidarAporteDto solicitud,
            [FromServices] ValidarAporte validarAporte,
            CancellationToken cancellationToken)
        {
            if (User.GetAuthenticatedUserId() is not { } userId)
                return Unauthorized();

            var aporte = await validarAporte.EjecutarAsync(slug, id, userId, solicitud.Confirma!.Value, cancellationToken);
            return Ok(aporte);
        }
    }
}
