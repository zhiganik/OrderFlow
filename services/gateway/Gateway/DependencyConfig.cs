using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using Microsoft.OpenApi;
using OrderFlow.Shared.Auth;
using OrderFlow.Shared.Middleware;
using OrderFlow.Shared.Swagger;
using Yarp.ReverseProxy.Transforms;

namespace Gateway;

public static class DependencyConfig
{
    public static void ConfigureDependencies(this WebApplicationBuilder builder)
    {
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddProblemDetails();

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();

        builder.Services.AddSwaggerGen(options => options.AddBearerSecurity());

        var jwtSection = builder.Configuration.GetSection("Jwt");
        builder.Services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = jwtSection["Issuer"],
                    ValidateAudience = true,
                    ValidAudience = jwtSection["Audience"],
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Convert.FromBase64String(jwtSection["SigningKey"]!)),
                    ValidateLifetime = true,
                };
            });
        builder.Services.AddAuthorization();

        builder.Services.AddReverseProxy()
            .LoadFromConfig(builder.Configuration.GetSection("ReverseProxy"))
            .AddTransforms(context =>
            {
                context.AddRequestTransform(transformContext =>
                {
                    transformContext.ProxyRequest.Headers.Remove(ForwardedHeaders.UserId);
                    transformContext.ProxyRequest.Headers.Remove(ForwardedHeaders.UserEmail);
                    transformContext.ProxyRequest.Headers.Remove(ForwardedHeaders.UserRoles);

                    var user = transformContext.HttpContext.User;
                    if (user.Identity?.IsAuthenticated == true)
                    {
                        var userId = user.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                        var email = user.FindFirst(JwtRegisteredClaimNames.Email)?.Value;
                        var roles = string.Join(',', user.FindAll(ClaimTypes.Role).Select(c => c.Value));

                        if (!string.IsNullOrEmpty(userId))
                        {
                            transformContext.ProxyRequest.Headers.Add(ForwardedHeaders.UserId, userId);
                        }

                        if (!string.IsNullOrEmpty(email))
                        {
                            transformContext.ProxyRequest.Headers.Add(ForwardedHeaders.UserEmail, email);
                        }

                        if (!string.IsNullOrEmpty(roles))
                        {
                            transformContext.ProxyRequest.Headers.Add(ForwardedHeaders.UserRoles, roles);
                        }
                    }

                    return ValueTask.CompletedTask;
                });
            });
    }
}
