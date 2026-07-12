using FluentValidation;
using MassTransit;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Microsoft.OpenApi;
using Order.Application.Dtos;
using Order.Application.Interfaces;
using Order.Application.Services;
using Order.Application.Validators;
using Order.Infrastructure.Persistence;
using Order.Infrastructure.Repositories;
using OrderFlow.Shared.Auth;
using OrderFlow.Shared.Middleware;
using OrderFlow.Shared.Swagger;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Extensions;

namespace Order.Api;

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
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "Order API", Version = "v1" });
            options.AddServer(new OpenApiServer { Url = "/order" });
            options.AddBearerSecurity();
        });

        builder.Services.AddHeaderAuthentication();

        builder.Services.AddStackExchangeRedisCache(options =>
            options.Configuration = builder.Configuration.GetConnectionString("Redis"));

        builder.Services.AddDbContext<OrderDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions
                    .MigrationsHistoryTable("__EFMigrationsHistory", "order")
                    .EnableRetryOnFailure()));

        builder.Services.AddScoped<IOrderRepository, OrderRepository>();
        builder.Services.AddScoped<IIdempotencyKeyRepository, IdempotencyKeyRepository>();
        builder.Services.AddScoped<IUnitOfWork, UnitOfWork>();
        builder.Services.AddScoped<IOrderService, OrderService>();

        builder.Services.AddValidatorsFromAssemblyContaining<CreateOrderRequestValidator>();
        builder.Services.AddFluentValidationAutoValidation();

        builder.Services.Configure<ServiceBusOptions>(builder.Configuration.GetSection("ServiceBus"));

        builder.Services.AddMassTransit(x =>
        {
            x.AddEntityFrameworkOutbox<OrderDbContext>(o =>
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
    }
}
