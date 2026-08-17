using AIForOrcas.Client.BL.Services;
using Microsoft.AspNetCore.Components.Server.Circuits;

namespace AIForOrcas.Client.Web.Extensions;

public static class Services
{
    public static void ConfigureDataServices(this WebApplicationBuilder builder)
    {
        // Register server-side token store as singleton.
        builder.Services.AddSingleton<ITokenStore, ServerSideTokenStore>();

        // Register circuit handler.
        builder.Services.AddScoped<CircuitHandlerService>();
        builder.Services.AddScoped<CircuitHandler>(sp => sp.GetRequiredService<CircuitHandlerService>());

        // Register authentication provider.
        builder.Services.AddScoped<ApiAuthenticationStateProvider>();
        builder.Services.AddScoped<AuthenticationStateProvider>(sp =>
            sp.GetRequiredService<ApiAuthenticationStateProvider>());

        // Register the scoped token provider that resolves the token from the active circuit.
        builder.Services.AddScoped<IAuthTokenProvider, CircuitAuthTokenProvider>();

        // Register HTTP clients with handlers. The 30s timeout keeps a hung API
        // from pinning a page on the default 100s before it can degrade.
        builder.Services.AddHttpClient("UnauthenticatedAPI", (sp, client) =>
        {
            var apiUrl = sp.GetRequiredService<AppSettings>().APIUrl;
            client.BaseAddress = new Uri(apiUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        builder.Services.AddHttpClient("AuthenticatedAPI", (sp, client) =>
        {
            var apiUrl = sp.GetRequiredService<AppSettings>().APIUrl;
            client.BaseAddress = new Uri(apiUrl);
            client.Timeout = TimeSpan.FromSeconds(30);
        });

        builder.Services.AddScoped<IDetectionService, DetectionService>();

        builder.Services.AddScoped<IMetricsService, MetricsService>();
        builder.Services.AddScoped<ITagService, TagService>();
        builder.Services.AddScoped<IAccountService, AccountService>();
    }
}
