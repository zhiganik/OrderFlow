namespace Gateway;

public static class ApplicationConfig
{
    public static void ConfigureApplication(this WebApplication app)
    {
        if (app.Environment.IsDevelopment())
        {
            app.UseSwagger();
            app.UseSwaggerUI();
        }

        app.UseHttpsRedirection();

        // No UseAuthentication/UseAuthorization here: the gateway is a pure YARP
        // proxy and forwards the Authorization header as-is. Identity/Order/Inventory
        // each validate the JWT themselves (they own the JwtBearer registration).

        app.MapControllers();
        app.MapReverseProxy();
    }
}
