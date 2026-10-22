using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.IdentityModel.JsonWebTokens;
using ZonaMatch.Api.Authentication;
using ZonaMatch.Application.DTOs.Auth.Requests;
using ZonaMatch.Application.DTOs.Auth.Responses;
using ZonaMatch.Application.UseCases.Auth;

namespace ZonaMatch.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class AuthController : ControllerBase
    {
        // POST /Auth/registro. The route is retained for existing clients.
        // Body: { "email": "", "password": "", "confirmPassword": "", "birthDate": "1990-05-31" }
        [HttpPost("registro")]
        [ProducesResponseType<UserResponse>(StatusCodes.Status201Created)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status409Conflict)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Register(
            [FromServices] RegisterUser registerUser,
            [FromBody] RegisterRequest request,
            CancellationToken cancellationToken)
        {
            var user = await registerUser.ExecuteAsync(request, cancellationToken);
            return StatusCode(StatusCodes.Status201Created, user);
        }

        // POST /Auth/login
        // Body: { "email": "", "password": "" }
        // The access token is returned as an HttpOnly cookie.
        [HttpPost("login")]
        [EnableRateLimiting("login")]
        [ProducesResponseType<LoginResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType<ValidationProblemDetails>(StatusCodes.Status400BadRequest)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status401Unauthorized)]
        [ProducesResponseType(StatusCodes.Status429TooManyRequests)]
        [ProducesResponseType<ProblemDetails>(StatusCodes.Status503ServiceUnavailable)]
        public async Task<IActionResult> Login(
            [FromServices] LoginUser loginUser,
            [FromBody] LoginRequest request,
            CancellationToken cancellationToken)
        {
            var loginResult = await loginUser.ExecuteAsync(request, cancellationToken);
            Response.Cookies.Append(
                AuthCookieDefaults.Name,
                loginResult.AccessToken,
                AuthCookieDefaults.CreateOptions(loginResult.ExpiresAt));

            return Ok(new LoginResponse(loginResult.ExpiresAt, loginResult.User));
        }

        // GET /Auth/me (authenticated by the HttpOnly cookie or an Authorization bearer token)
        [Authorize]
        [HttpGet("me")]
        [ProducesResponseType<CurrentSessionResponse>(StatusCodes.Status200OK)]
        [ProducesResponseType(StatusCodes.Status401Unauthorized)]
        public IActionResult Me()
        {
            var userIdClaim = User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
            var email = User.FindFirst(JwtRegisteredClaimNames.Email)?.Value;

            if (!Guid.TryParse(userIdClaim, out var userId) || email is null)
                return Unauthorized();

            return Ok(new CurrentSessionResponse(userId, email));
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
