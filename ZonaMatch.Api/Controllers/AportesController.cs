using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Api.Controllers
{
    // Neighbors' contributions to the map of a zone: new points and corrections. They stay pending until
    // other neighbors validate them; approved ones show up in the points of interest endpoints.
    [ApiController]
    [Route("Zonas/{slug:regex(" + RutasZona.Slug + ")}/aportes")]
    public class AportesController : ControllerBase
    {
        private readonly IAporteService _aporteService;

        public AportesController(IAporteService aporteService)
        {
            _aporteService = aporteService;
        }

        // GET /Zonas/villa-luro/aportes/resumen
        [HttpGet("resumen")]
        [ProducesResponseType<ResumenAportesDto>(StatusCodes.Status200OK)]
        public async Task<IActionResult> Resumir(string slug, CancellationToken cancellationToken) =>
            Ok(await _aporteService.ResumirAsync(slug, cancellationToken));

        // GET /Zonas/villa-luro/aportes/pendientes
        // Oldest first. With a token, each one says whether it is yours (esMio) and your vote (miVoto).
        [HttpGet("pendientes")]
        [ProducesResponseType<IReadOnlyList<AporteDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> ListarPendientes(string slug, CancellationToken cancellationToken) =>
            Ok(await _aporteService.ListarPendientesAsync(slug, User.UsuarioId(), cancellationToken));

        // POST /Zonas/villa-luro/aportes/puntos
        // Body: { "categoria": "salud", "nombre": "", "horario": "", "direccion": "", "latitud": -34.63,
        //         "longitud": -58.50, "viveOTrabajaEnZona": true }
        [Authorize]
        [HttpPost("puntos")]
        [ProducesResponseType<AporteDto>(StatusCodes.Status201Created)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CrearPunto(string slug, CrearPuntoNuevoDto request, CancellationToken cancellationToken)
        {
            if (User.Sesion() is not { } sesion)
                return Unauthorized();

            var aporte = await _aporteService.CrearPuntoAsync(slug, sesion, request, cancellationToken);
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
        public async Task<IActionResult> CrearCorreccion(string slug, CrearCorreccionDto request, CancellationToken cancellationToken)
        {
            if (User.Sesion() is not { } sesion)
                return Unauthorized();

            var aporte = await _aporteService.CrearCorreccionAsync(slug, sesion, request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, aporte);
        }

        // PUT /Zonas/villa-luro/aportes/{id}/validacion    Body: { "confirma": true }
        // Your vote on a pending contribution of another user; voting again changes it
        [Authorize]
        [HttpPut("{id:guid}/validacion")]
        [ProducesResponseType<AporteDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Validar(string slug, Guid id, ValidarAporteDto request, CancellationToken cancellationToken)
        {
            if (User.UsuarioId() is not { } usuarioId)
                return Unauthorized();

            var aporte = await _aporteService.ValidarAsync(slug, id, usuarioId, request.Confirma!.Value, cancellationToken);
            return Ok(aporte);
        }
    }
}
