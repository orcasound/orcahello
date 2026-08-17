namespace OrcaHello.Web.UI.Models
{
    [ExcludeFromCodeCoverage]
    public static class AuthenticationServiceProviders
    {
        // Set ModeratorPolicy
        public static void ConfigureModeratorPolicy(this WebApplicationBuilder builder, AppSettings appSettings)
        {
            if (!string.IsNullOrWhiteSpace(appSettings?.AzureAd?.AzureAdGroups?.ModeratorGroupId))
            {
                builder.Services.AddAuthorization(options =>
                {
                    options.AddPolicy("Moderators", policyBuilder =>
                        policyBuilder.RequireClaim("groups", appSettings.AzureAd.AzureAdGroups.ModeratorGroupId));
                });
            }
        }

        // Inject service providers
        public static void ConfigureAuthProviders(this WebApplicationBuilder builder)
        {
            builder.Services.AddScoped<ApiAuthenticationStateProvider>();
            builder.Services.AddScoped<AuthenticationStateProvider>(provider =>
                provider.GetRequiredService<ApiAuthenticationStateProvider>());
            builder.Services.AddScoped<IAccountService, AccountService>();
        }
    }
}
