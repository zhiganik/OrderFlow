using System.IdentityModel.Tokens.Jwt;
using Serilog;
using Yarp.ReverseProxy.Model;

namespace Gateway;

public static class ApplicationConfig
{
    public static void ConfigureApplication(this WebApplication app)
    {
        app.UseExceptionHandler();
        app.UseStatusCodePages();

        app.UseSerilogRequestLogging(options =>
        {
            options.EnrichDiagnosticContext = (diagnosticContext, httpContext) =>
            {
                // Only set for requests that actually reached the proxy — auth/authorization
                // failures short-circuit before this feature exists.
                var proxyFeature = httpContext.Features.Get<IReverseProxyFeature>();
                if (proxyFeature is not null)
                {
                    diagnosticContext.Set("RouteId", proxyFeature.Route.Config.RouteId);
                    diagnosticContext.Set("ClusterId", proxyFeature.Cluster?.Config.ClusterId);
                    diagnosticContext.Set("Destination", proxyFeature.ProxiedDestination?.Model.Config.Address);
                }

                var userId = httpContext.User.FindFirst(JwtRegisteredClaimNames.Sub)?.Value;
                if (!string.IsNullOrEmpty(userId))
                {
                    diagnosticContext.Set("UserId", userId);
                }
            };
        });

        app.UseHttpsRedirection();

        app.UseAuthentication();
        app.UseAuthorization();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI(options =>
            {
                options.SwaggerEndpoint("/identity/swagger/v1/swagger.json", "Identity API (v1)");
                options.SwaggerEndpoint("/order/swagger/v1/swagger.json", "Order API (v1)");
                options.SwaggerEndpoint("/inventory/swagger/v1/swagger.json", "Inventory API (v1)");
            });
        }

        app.MapControllers();
        app.MapReverseProxy();
    }
}
