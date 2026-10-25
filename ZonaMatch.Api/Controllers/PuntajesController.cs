using System.Diagnostics;
using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.UseCases.Puntajes;

namespace ZonaMatch.Api.Controllers
{
    // Motor de puntaje: que tanto le cierra cada zona a un usuario segun su criterio.
    // Publico: el visitante tambien ve el resultado de su analisis (el criterio viaja en el pedido).
    [ApiController]
    [Route("[controller]")]
    public class PuntajesController : ControllerBase
    {
        // Above this the request is logged as a warning so slow scoring (usually MOTIS) is easy to spot
        private const long SlowScoringMilliseconds = 5_000;

        private readonly CalcularPuntajes _calcularPuntajes;
        private readonly ILogger<PuntajesController> _logger;

        public PuntajesController(CalcularPuntajes calcularPuntajes, ILogger<PuntajesController> logger)
        {
            _calcularPuntajes = calcularPuntajes;
            _logger = logger;
        }

        // POST /Puntajes
        // Body: { "zonaInicial": "villa-luro", "zonasAgregadas": [], "criterios": { "seguridad": "alta" },
        //         "lugares": [{ "nombre": "Trabajo", "latitude": -34.6, "longitude": -58.38, "prioridad": "Alta", "tiempoMax": "45 min" }],
        //         "alquilerMaximo": "800000", "movilidad": ["transporte-publico"] }
        [HttpPost]
        [ProducesResponseType<PuntajesDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status404NotFound)]
        public async Task<IActionResult> Calcular(CalcularPuntajesRequest solicitud, CancellationToken cancellationToken)
        {
            var reloj = Stopwatch.StartNew();
            var resultado = await _calcularPuntajes.EjecutarAsync(solicitud, cancellationToken);
            reloj.Stop();

            _logger.Log(
                reloj.ElapsedMilliseconds > SlowScoringMilliseconds ? LogLevel.Warning : LogLevel.Information,
                "Scores calculated in {ElapsedMs} ms", reloj.ElapsedMilliseconds);

            return Ok(resultado);
        }
    }
}
