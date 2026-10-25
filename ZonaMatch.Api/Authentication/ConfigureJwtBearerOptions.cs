using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;

namespace ZonaMatch.Api.Authentication
{
    // Validation rules applied to every bearer token or authentication cookie accepted by the middleware.
    // Done here (instead of inline in Program.cs) so the rules read the validated JwtOptions.
    public sealed class ConfigureJwtBearerOptions(IOptions<JwtOptions> jwtOptions) : IConfigureNamedOptions<JwtBearerOptions>
    {
        public void Configure(string? name, JwtBearerOptions options)
        {
            if (name != JwtBearerDefaults.AuthenticationScheme)
                return;

            var jwt = jwtOptions.Value;

            // Keep the original claim names ("sub", "email") instead of the legacy ClaimTypes.* mapping
            options.MapInboundClaims = false;

            options.TokenValidationParameters = new TokenValidationParameters
            {
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = jwt.CreateSigningKey(),
                // Only the algorithm we sign with: prevents algorithm-confusion attacks
                ValidAlgorithms = [SecurityAlgorithms.HmacSha256],

                ValidateIssuer = true,
                ValidIssuer = jwt.Issuer,
                ValidateAudience = true,
                ValidAudience = jwt.Audience,

                RequireExpirationTime = true,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromSeconds(30),

                NameClaimType = JwtRegisteredClaimNames.Email,
                RoleClaimType = AppRoles.ClaimType
            };

            options.Events = new JwtBearerEvents
            {
                OnMessageReceived = context =>
                {
                    var authorization = context.Request.Headers["Authorization"].ToString();
                    var hasBearerToken = authorization.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase);

                    if (!hasBearerToken && context.Request.Cookies.TryGetValue(AuthCookieDefaults.Name, out var token))
                        context.Token = token;

                    return Task.CompletedTask;
                }
            };
        }

        public void Configure(JwtBearerOptions options) => Configure(Options.DefaultName, options);
    }
}
