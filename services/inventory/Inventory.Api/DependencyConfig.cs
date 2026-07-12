using Inventory.Application.Dtos;
using Inventory.Infrastructure.Persistence;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;

namespace Inventory.Api;

public static class DependencyConfig
{
    public static void ConfigureDependencies(this WebApplicationBuilder builder)
    {
        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "Inventory API", Version = "v1" });
            options.AddServer(new OpenApiServer { Url = "/inventory" });
            
            options.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
            {
                Name = "Authorization",
                In = ParameterLocation.Header,
                Type = SecuritySchemeType.Http,
                Scheme = "Bearer",
                BearerFormat = "JWT",
            });
            options.AddSecurityRequirement(document => new OpenApiSecurityRequirement
            {
                { new OpenApiSecuritySchemeReference("Bearer", document), new List<string>() },
            });
        });

        builder.Services.AddStackExchangeRedisCache(options =>
            options.Configuration = builder.Configuration.GetConnectionString("Redis"));

        builder.Services.Configure<ServiceBusOptions>(builder.Configuration.GetSection("ServiceBus"));

        builder.Services.AddDbContext<InventoryDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions
                    .MigrationsHistoryTable("__EFMigrationsHistory", "inventory")
                    .EnableRetryOnFailure()));

        builder.Services.AddMassTransit(x =>
        {
            x.AddEntityFrameworkOutbox<InventoryDbContext>(o =>
            {
                o.UseSqlServer();
                o.UseBusOutbox();
            });

            x.UsingAzureServiceBus((context, cfg) =>
            {
                var serviceBusOptions = context.GetRequiredService<IOptions<ServiceBusOptions>>().Value;
                cfg.Host(serviceBusOptions.ConnectionString);
            });
        });

        // TODO: register repositories (interface -> implementation)
        // TODO: register FluentValidation validators
        // TODO: register application services
    }
}
