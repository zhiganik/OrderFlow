namespace Identity.Api;

public static class DependencyConfig
{
    public static void ConfigureDependencies(this WebApplicationBuilder builder)
    {
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(/* add JWT security definition here later */);

        // TODO: bind IOptions<T> sections via builder.Configuration.GetSection(...)
        // TODO: register DbContext
        // TODO: register repositories (interface -> implementation)
        // TODO: register FluentValidation validators
        // TODO: register JWT authentication + authorization
        // TODO: register application services
    }
}
