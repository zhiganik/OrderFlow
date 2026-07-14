using OrderFlow.Shared.Middleware;
using Serilog;

namespace Order.Api;

public static class ApplicationConfig
{
    public static void ConfigureApplication(this WebApplication app)
    {
        app.UseExceptionHandler();

        app.UseMiddleware<CorrelationIdMiddleware>();
        app.UseSerilogRequestLogging();

        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        app.UseAuthentication();
        app.UseAuthorization();

        app.MapControllers();
    }
}
