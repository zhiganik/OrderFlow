using FluentValidation;
using Identity.Api.Startup;
using Identity.Application.Domain;
using Identity.Application.Dtos;
using Identity.Application.Interfaces;
using Identity.Application.Services;
using Identity.Application.Validators;
using Identity.Infrastructure.Persistence;
using Identity.Infrastructure.Repositories;
using Microsoft.AspNetCore.Identity;
using Microsoft.EntityFrameworkCore;
using Microsoft.OpenApi;
using OrderFlow.Shared.Middleware;
using OrderFlow.Shared.Swagger;
using SharpGrip.FluentValidation.AutoValidation.Mvc.Extensions;

namespace Identity.Api;

public static class DependencyConfig
{
    public static void ConfigureDependencies(this WebApplicationBuilder builder)
    {
        builder.Services.AddExceptionHandler<GlobalExceptionHandler>();
        builder.Services.AddProblemDetails();

        builder.Services.AddControllers();
        builder.Services.AddEndpointsApiExplorer();

        builder.Services.AddStackExchangeRedisCache(options =>
            options.Configuration = builder.Configuration.GetConnectionString("Redis"));

        builder.Services.Configure<JwtOptions>(builder.Configuration.GetSection("Jwt"));

        builder.Services.AddDbContext<ApplicationIdentityDbContext>(options =>
            options.UseSqlServer(builder.Configuration.GetConnectionString("DefaultConnection"),
                sqlOptions => sqlOptions
                    .MigrationsHistoryTable("__EFMigrationsHistory", "identity")
                    .EnableRetryOnFailure()));

        builder.Services.AddDataProtection();
        builder.Services.AddIdentityCore<ApplicationUser>()
            .AddRoles<IdentityRole>()
            .AddEntityFrameworkStores<ApplicationIdentityDbContext>()
            .AddDefaultTokenProviders();

        builder.Services.AddScoped<IRefreshTokenStore, RedisRefreshTokenStore>();
        builder.Services.AddScoped<IAuthService, AuthService>();
        builder.Services.AddHostedService<RoleSeeder>();

        builder.Services.AddValidatorsFromAssemblyContaining<RegisterRequestValidator>();
        builder.Services.AddFluentValidationAutoValidation();

        builder.Services.AddSwaggerGen(options =>
        {
            options.SwaggerDoc("v1", new OpenApiInfo { Title = "Identity API", Version = "v1" });
            options.AddServer(new OpenApiServer { Url = "/identity" });
            options.AddBearerSecurity();
        });
    }
}
