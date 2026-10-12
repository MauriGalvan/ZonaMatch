using Microsoft.AspNetCore.Mvc;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.UseCases.Criterios;

namespace ZonaMatch.Api.Controllers
{
    // Catalogo de criterios del wizard de analisis. Publico: tambien lo necesita el wizard de invitado.
    [ApiController]
    [Route("[controller]")]
    public class CriteriosController : ControllerBase
    {
        private readonly ListarCriterios _listarCriterios;

        public CriteriosController(ListarCriterios listarCriterios)
        {
            _listarCriterios = listarCriterios;
        }

        // GET /Criterios
        [HttpGet]
        [ProducesResponseType<IReadOnlyList<CriterioDto>>(StatusCodes.Status200OK)]
        public async Task<IActionResult> ObtenerTodos(CancellationToken cancellationToken)
        {
            var criterios = await _listarCriterios.EjecutarAsync(cancellationToken);
            return Ok(criterios);
        }
    }
}
