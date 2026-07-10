using Microsoft.AspNetCore.Identity;
using OrderFlow.Shared.Auth;

namespace Identity.Api.Startup;

// Ensures the roles the JWT/policy layer depends on exist. There is no
// role-assignment endpoint yet — assign a user to "Admin" manually via
// `make sql-shell` until one is built.
public class RoleSeeder(IServiceScopeFactory scopeFactory) : IHostedService
{
    private static readonly string[] SeedRoles = [AuthPolicies.Admin];

    public async Task StartAsync(CancellationToken cancellationToken)
    {
        using var scope = scopeFactory.CreateScope();
        var roleManager = scope.ServiceProvider.GetRequiredService<RoleManager<IdentityRole>>();

        foreach (var role in SeedRoles)
        {
            if (!await roleManager.RoleExistsAsync(role))
            {
                await roleManager.CreateAsync(new IdentityRole(role));
            }
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
