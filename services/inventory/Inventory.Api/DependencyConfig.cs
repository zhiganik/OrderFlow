using FluentValidation;
using Inventory.Application.Dtos;
using Inventory.Application.Interfaces;
using Inventory.Application.Services;
using Inventory.Application.Validators;
using Inventory.Infrastructure.Messaging;
using Inventory.Infrastructure.Persistence;
using Inventory.Infrastructure.Repositories;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using OrderFlow.Shared.Auth;
using OrderFlow.Shared.Middleware;
using OrderFlow.Shared.Swagger;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Extensions;

namespace Inventory.Api;

public static class DependencyConfig
{
    public static void ConfigureDependencies(this WebApplicationBuilder builder)
    {
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddProblemDetails();

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();
        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "Inventory API", Version = "v1" });
            options.AddServer(new OpenApiServer { Url = "/inventory" });
            options.AddBearerSecurity();
        });

        builder.Services.AddHeaderAuthentication();

        builder.Services.AddStackExchangeRedisCache(options =>
            options.Configuration = builder.Configuration.GetConnectionString("Redis"));

        builder.Services.Configure<ServiceBusOptions>(builder.Configuration.GetSection("ServiceBus"));
        builder.Services.Configure<RabbitMqOptions>(builder.Configuration.GetSection("RabbitMq"));

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

            x.AddConsumer<OrderCreatedConsumer>();

            x.AddConfigureEndpointsCallback((context, _, cfg) => cfg.UseEntityFrameworkOutbox<InventoryDbContext>(context));

            if (builder.Environment.IsDevelopment())
            {
                x.UsingRabbitMq((context, cfg) =>
                {
                    var rabbitMqOptions = context.GetRequiredService<IOptions<RabbitMqOptions>>().Value;
                    cfg.Host(rabbitMqOptions.Host, "/", h =>
                    {
                        h.Username(rabbitMqOptions.Username);
                        h.Password(rabbitMqOptions.Password);
                    });

                    cfg.ConfigureEndpoints(context);
                });
            }
            else
            {
                x.UsingAzureServiceBus((context, cfg) =>
                {
                    var serviceBusOptions = context.GetRequiredService<IOptions<ServiceBusOptions>>().Value;
                    cfg.Host(serviceBusOptions.ConnectionString);

                    cfg.ConfigureEndpoints(context);
                });
            }
        });

        builder.Services.AddScoped<IStockItemRepository, StockItemRepository>();
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        builder.Services.AddScoped<IStockService, StockService>();
        builder.Services.AddScoped<IStockReservationService, StockReservationService>();

        builder.Services.AddValidatorsFromAssemblyContaining<UpsertStockItemRequestValidator>();
        builder.Services.AddFluentValidationAutoValidation();
    }
}
