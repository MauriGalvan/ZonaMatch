using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.UseCases.Aportes;

namespace ZonaMatch.Api.Controllers
{
    // Aportes de los vecinos al mapa de una zona: puntos nuevos y correcciones. Quedan pendientes hasta que
    // otros vecinos los validan; los aprobados aparecen en los endpoints de puntos de interes.
    [ApiController]
    [Route("Zonas/{slug:regex(" + RutasZona.Slug + ")}/aportes")]
    public class AportesController : ControllerBase
    {
        private readonly ResumirAportes _resumirAportes;
        private readonly ListarAportesPendientes _listarAportesPendientes;
        private readonly CrearPuntoNuevo _crearPuntoNuevo;
        private readonly CrearCorreccion _crearCorreccion;
        private readonly ValidarAporte _validarAporte;

        public AportesController(
            ResumirAportes resumirAportes,
            ListarAportesPendientes listarAportesPendientes,
            CrearPuntoNuevo crearPuntoNuevo,
            CrearCorreccion crearCorreccion,
            ValidarAporte validarAporte)
        {
            _resumirAportes = resumirAportes;
            _listarAportesPendientes = listarAportesPendientes;
            _crearPuntoNuevo = crearPuntoNuevo;
            _crearCorreccion = crearCorreccion;
            _validarAporte = validarAporte;
        }

        // GET /Zonas/villa-luro/aportes/resumen
        [HttpGet("resumen")]
        [ProducesResponseType<ResumenAportesDto>(StatusCodes.Status200OK)]
        public async Task<IActionResult> Resumir(string slug, CancellationToken cancellationToken) =>
            Ok(await _resumirAportes.EjecutarAsync(slug, cancellationToken));

        // GET /Zonas/villa-luro/aportes/pendientes
        // El mas viejo primero. Con token, cada uno indica si es tuyo (esMio) y tu voto (miVoto).
        [HttpGet("pendientes")]
        [ProducesResponseType<IReadOnlyList<AporteDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> ListarPendientes(string slug, CancellationToken cancellationToken) =>
            Ok(await _listarAportesPendientes.EjecutarAsync(slug, User.UsuarioId(), cancellationToken));

        // POST /Zonas/villa-luro/aportes/puntos
        // Cuerpo: { "categoria": "salud", "nombre": "", "horario": "", "direccion": "", "latitud": -34.63,
        //         "longitud": -58.50, "viveOTrabajaEnZona": true }
        [Authorize]
        [HttpPost("puntos")]
        [ProducesResponseType<AporteDto>(StatusCodes.Status201Created)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CrearPunto(string slug, CrearPuntoNuevoDto solicitud, CancellationToken cancellationToken)
        {
            if (User.Sesion() is not { } sesion)
                return Unauthorized();

            var aporte = await _crearPuntoNuevo.EjecutarAsync(slug, sesion, solicitud, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, aporte);
        }

        // POST /Zonas/villa-luro/aportes/correcciones
        // Cuerpo: { "puntoInteresId": "n123", "puntoInteresNombre": "", "motivo": "cerro|nombre|ubicacion|categoria|otro",
        //         "nombrePropuesto": "", "categoriaPropuesta": "", "latitud": 0, "longitud": 0, "comentario": "" }
        [Authorize]
        [HttpPost("correcciones")]
        [ProducesResponseType<AporteDto>(StatusCodes.Status201Created)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> CrearCorreccion(string slug, CrearCorreccionDto solicitud, CancellationToken cancellationToken)
        {
            if (User.Sesion() is not { } sesion)
                return Unauthorized();

            var aporte = await _crearCorreccion.EjecutarAsync(slug, sesion, solicitud, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, aporte);
        }

        // PUT /Zonas/villa-luro/aportes/{id}/validacion    Cuerpo: { "confirma": true }
        // Tu voto sobre un aporte pendiente de otro usuario; votar de nuevo lo cambia
        [Authorize]
        [HttpPut("{id:guid}/validacion")]
        [ProducesResponseType<AporteDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        public async Task<IActionResult> Validar(string slug, Guid id, ValidarAporteDto solicitud, CancellationToken cancellationToken)
        {
            if (User.UsuarioId() is not { } usuarioId)
                return Unauthorized();

            var aporte = await _validarAporte.EjecutarAsync(slug, id, usuarioId, solicitud.Confirma!.Value, cancellationToken);
            return Ok(aporte);
        }
    }
}
