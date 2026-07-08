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

        // TODO: app.UseAuthentication();
        // TODO: app.UseAuthorization();

        app.MapControllers();
    }
}
