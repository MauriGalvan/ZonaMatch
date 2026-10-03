using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;
using ZonaMatch.Api.Infrastructure;
using ZonaMatch.Application.Ingestion;

namespace ZonaMatch.Api.Controllers
{
    // dispara la ingesta desde las tablas de origen al modelo canónico, en este orden:
    // territorial -> zones -> pois -> deduplicate
    [ApiController]
    [Route("api/admin/ingestion")]
    [Produces("application/json")]
    public class IngestionController : ControllerBase
    {
        private readonly AdminOptions _options;

        public IngestionController(IOptions<AdminOptions> options)
        {
            _options = options.Value;
        }

        [HttpPost("territorial")]
        public Task<IActionResult> Territorial([FromServices] TerritorialImportService service, CancellationToken cancellationToken) =>
            RunAsync(() => service.ImportAsync(cancellationToken));

        [HttpPost("zones")]
        public Task<IActionResult> Zones([FromServices] ZoneBuildService service, CancellationToken cancellationToken) =>
            RunAsync(() => service.RebuildAsync(cancellationToken));

        [HttpPost("pois")]
        public Task<IActionResult> Pois([FromServices] PoiImportService service, CancellationToken cancellationToken) =>
            RunAsync(() => service.ImportAsync(cancellationToken));

        [HttpPost("deduplicate")]
        public Task<IActionResult> Deduplicate([FromServices] PoiDeduplicationService service, CancellationToken cancellationToken) =>
            RunAsync(() => service.RunAsync(cancellationToken));

        private async Task<IActionResult> RunAsync<TReport>(Func<Task<TReport>> run)
        {
            if (!_options.IngestionEndpointsEnabled)
            {
                return NotFound();
            }

            return Ok(await run());
        }
    }
}
