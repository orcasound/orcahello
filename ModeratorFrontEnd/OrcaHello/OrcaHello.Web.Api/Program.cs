[ExcludeFromCodeCoverage]
internal class Program
{
    private static void Main(string[] args)
    {
        var builder = WebApplication.CreateBuilder(args);

        // Add AppSettings

        var appSettings = new AppSettings();
        builder.Configuration.GetSection("AppSettings").Bind(appSettings);
        builder.Services.AddSingleton(appSettings);

        // Add services to the container

        ComponentDependencyInjector.AddBrokers(builder);
        ComponentDependencyInjector.AddFoundationServices(builder);
        ComponentDependencyInjector.AddOrchestrationServices(builder);

        // Add authentication/authorization middleware 

        ComponentDependencyInjector.ConfigureCors(builder);
        ComponentDependencyInjector.ConfigureJwtAuthentication(builder);
        ComponentDependencyInjector.ConfigureModeratorPolicy(builder, appSettings);

        // Add Swagger configiration

        ComponentDependencyInjector.ConfigureSwagger(builder, appSettings);

        // Add built-in middleware

        builder.Services.AddControllers().ConfigureApiBehaviorOptions(x => { x.SuppressMapClientErrors = true; });
        builder.Services.AddEndpointsApiExplorer();

        var app = builder.Build();

        // Add Swagger to the HTTP request pipeline

        app.UseSwagger();

        app.UseSwaggerUI(c =>
        {
            var clientId = !string.IsNullOrWhiteSpace(appSettings.AzureAd.ClientId) ?
                appSettings.AzureAd.ClientId : Guid.NewGuid().ToString();

            c.OAuthClientId(clientId);
            c.OAuthUsePkce();
            c.OAuthScopeSeparator(" ");
            c.DefaultModelsExpandDepth(-1);
        });

        app.UseHttpsRedirection();

        // Add the CORS policy to the HTTP Request pipeline

        app.UseCors("AllowAnyOrigin");

        // Add authentication and authorization to the HTTP Request pipeline

        app.UseAuthentication();
        app.UseAuthorization();

        // Add the API endpoint controllers to the HTTP Request pipeline

        app.MapControllers();

        app.Run();
    }
}