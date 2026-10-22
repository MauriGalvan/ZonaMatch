using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs;
using ZonaMatch.Application.Interfaces;

namespace ZonaMatch.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {
        private readonly IAuthService _authService;

        public AuthController(IAuthService authService)
        {
            _authService = authService;
        }

        // POST /Auth/registro
        // Body: { "email": "", "password": "", "confirmarPassword": "", "fechaNacimiento": "1990-05-31" }
        [HttpPost("registro")]
        [ProducesResponseType<UsuarioDto>(StatusCodes.Status201Created)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Registrar(RegistroUsuarioDto request, CancellationToken cancellationToken)
        {
            var usuario = await _authService.RegistrarAsync(request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, usuario);
        }

        // POST /Auth/login
        // Body: { "email": "", "password": "" }
        // Response: { "expiraEn": "...", "usuario": { ... } }; the access token is an HttpOnly cookie.
        [HttpPost("login")]
        [EnableRateLimiting("login")]
        [ProducesResponseType<LoginResponseDto>(StatusCodes.Status200OK)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Login(LoginDto request, CancellationToken cancellationToken)
        {
            var respuesta = await _authService.LoginAsync(request, cancellationToken);
            Response.Cookies.Append(
                AuthCookieDefaults.Name,
                respuesta.AccessToken,
                AuthCookieDefaults.CreateOptions(respuesta.ExpiraEn));

            return Ok(new LoginResponseDto(respuesta.ExpiraEn, respuesta.Usuario));
        }

        // GET /Auth/me (authenticated by the HttpOnly cookie or an Authorization bearer token)
        [Authorize]
        [HttpGet("me")]
        [ProducesResponseType<SesionDto>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult Me()
        {
            var id = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            var email = User.FindFirst(JwtRegisteredClaimNames.Email)?.Value;

            if (!Guid.TryParse(id, out var usuarioId) || email is null)
                return Unauthorized();

            return Ok(new SesionDto(usuarioId, email));
        }

        [HttpPost("logout")]
        [ProducesResponseType(StatusCodes.Status204NoContent)]
        [AllowAnonymous]
        public IActionResult Logout()
        {
            Response.Cookies.Delete(AuthCookieDefaults.Name, AuthCookieDefaults.CreateOptions());
            return NoContent();
        }
    }
}
