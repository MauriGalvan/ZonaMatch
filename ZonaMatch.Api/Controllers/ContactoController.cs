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
        private readonly EnviarFormularioContacto _enviarFormulario;

        public ContactoController(EnviarFormularioContacto enviarFormulario)
        {
            _enviarFormulario = enviarFormulario;
        }

        // POST /Contacto    Cuerpo: { "email": "...", "nombre": "...", "zona": "...", "sitio": "" }
        [HttpPost]
        [EnableRateLimiting("contacto")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Enviar(FormularioContactoDto solicitud, CancellationToken cancellationToken)
        {
            await _enviarFormulario.EjecutarAsync(solicitud, cancellationToken);
            return NoContent();
        }
    }
}
