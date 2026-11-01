using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.UseCases.Contacto;

namespace ZonaMatch.Api.Controllers
{
    // Formulario de contacto de la landing: es publico y cada envio manda un mail al equipo
    [ApiController]
    [Route("Contacto")]
    public class ContactoController : ControllerBase
    {
        // POST /Contacto    Cuerpo: { "email": "...", "nombre": "...", "zona": "...", "sitio": "" }
        [HttpPost]
        [EnableRateLimiting("contacto")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Enviar(
            [FromBody] FormularioContactoDto solicitud,
            [FromServices] EnviarFormularioContacto useCase,
            CancellationToken cancellationToken)
        {
            await useCase.EjecutarAsync(solicitud, cancellationToken);
            return NoContent();
        }
    }
}
