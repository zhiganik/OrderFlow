using Inventory.Api;
using Serilog;

Log.Logger = new LoggerConfiguration()
    .WriteTo.Console()
    .CreateBootstrapLogger();

try
{
    Log.Information("Starting Inventory.Api");

    var builder = WebApplication.CreateBuilder(args);

    builder.Host.UseSerilog((context, services, configuration) => configuration
        .ReadFrom.Configuration(context.Configuration)
        .ReadFrom.Services(services)
        .Enrich.FromLogContext());

    builder.ConfigureDependencies();

    var app = builder.Build();
    app.ConfigureApplication();

    app.Run();
}
catch (Exception ex)
{
    Log.Fatal(ex, "Inventory.Api terminated unexpectedly");
}
finally
{
    Log.CloseAndFlush();
}
