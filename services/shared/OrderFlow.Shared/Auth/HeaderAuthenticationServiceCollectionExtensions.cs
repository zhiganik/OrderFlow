using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.DependencyInjection;

namespace OrderFlow.Shared.Auth;

public static class HeaderAuthenticationServiceCollectionExtensions
{
    public static IServiceCollection AddHeaderAuthentication(this IServiceCollection services)
    {
        services.AddHttpContextAccessor();
        services.AddScoped<ICurrentUser, CurrentUser>();

        services.AddAuthentication(HeaderAuthenticationDefaults.Scheme)
            .AddScheme<AuthenticationSchemeOptions, HeaderAuthenticationHandler>(HeaderAuthenticationDefaults.Scheme, _ => { });

        services.AddAuthorizationBuilder()
            .AddPolicy(AuthPolicies.Admin, policy => policy.RequireRole(AuthPolicies.Admin));

        return services;
    }
}
