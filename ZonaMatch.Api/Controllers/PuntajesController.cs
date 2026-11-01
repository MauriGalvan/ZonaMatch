using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Exceptions;
using ZonaMatch.Application.UseCases.Puntajes;

namespace ZonaMatch.Api.Controllers
{
    // Motor de puntaje: que tanto le cierra cada zona a un usuario segun su criterio.
    // Publico: el visitante tambien ve el resultado de su analisis (el criterio viaja en el pedido).
    [ApiController]
    [Route("api/v1/scores")]
    public class PuntajesController : ControllerBase
    {
        // Above this the request is logged as a warning so slow scoring (usually MOTIS) is easy to spot
        private const long SlowScoringMilliseconds = 5_000;

        // POST /api/v1/scores
        // Body: { "zonaInicial": "villa-luro", "zonasAgregadas": [], "criterios": { "seguridad": "alta" },
        //         "lugares": [{ "nombre": "Trabajo", "latitude": -34.6, "longitude": -58.38, "prioridad": "Alta", "tiempoMax": "45 min" }],
        //         "alquilerMaximo": "800000", "movilidad": ["transporte-publico"] }
        [HttpPost]
        [ProducesResponseType<PuntajesDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Calcular(
            [FromBody] CalcularPuntajesRequest solicitud,
            [FromServices] CalcularPuntajes useCase,
            [FromServices] ILogger<PuntajesController> logger,
            CancellationToken cancellationToken)
        {
            // The visitor tries it with a few places; with an account, up to 15 (checked by the request)
            if (User.GetAuthenticatedUserId() is null && solicitud.Lugares.Count > LimitesLugares.Visitante)
                throw new BadRequestException(LimitesLugares.MensajeVisitante);

            // Saved analyses go straight to the use case, so older ones without limits still recalculate
            LimitesObligatorios.Validar(
                solicitud.AlquilerMaximo, solicitud.Operacion, solicitud.CompraMaximaUsd, solicitud.Lugares.Select(lugar => lugar.TiempoMax));

            var reloj = Stopwatch.StartNew();
            var resultado = await useCase.EjecutarAsync(solicitud, cancellationToken);
            reloj.Stop();

            logger.Log(
                reloj.ElapsedMilliseconds > SlowScoringMilliseconds ? LogLevel.Warning : LogLevel.Information,
                "Scores calculated in {ElapsedMs} ms", reloj.ElapsedMilliseconds);

            return Ok(resultado);
        }
    }
}
