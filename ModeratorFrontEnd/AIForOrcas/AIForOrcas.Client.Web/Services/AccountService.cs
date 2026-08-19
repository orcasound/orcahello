using AIForOrcas.Client.Web.Models;
using Blazorade.Msal.Services;
using Microsoft.AspNetCore.Components.Authorization;
using Microsoft.Extensions.Logging;

namespace AIForOrcas.Client.Web.Services;

public class AccountService : IAccountService
{
    private readonly AuthenticationStateProvider _authenticationStateProvider;
    private readonly BlazoradeMsalService _msalService;
    private readonly AppSettings _appSettings;
    private readonly ILogger<AccountService> _logger;

    public AccountService(
        AuthenticationStateProvider authenticationStateProvider,
        BlazoradeMsalService msalService,
        AppSettings appSettings,
        ILogger<AccountService> logger)
    {
        _authenticationStateProvider = authenticationStateProvider;
        _msalService = msalService;
        _appSettings = appSettings;
        _logger = logger;
    }

    public string GetToken()
    {
        if (_authenticationStateProvider is ApiAuthenticationStateProvider apiProvider)
        {
            return apiProvider.GetToken();
        }
        return null;
    }

    public async Task<string> GetDisplayname()
    {
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (user?.Identity?.IsAuthenticated == true)
        {
            var name = user.FindFirst(c => c.Type == "name")?.Value;
            var identity = user.Identity.Name;
            return string.IsNullOrWhiteSpace(name) ? identity : name;
        }

        return string.Empty;
    }

    public async Task<string> GetUsername()
    {
        var authState = await _authenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;

        if (user?.Identity?.IsAuthenticated == true)
        {
            // Try preferred_username first (Azure AD v2.0 tokens)
            var username = user.FindFirst(c => c.Type == "preferred_username")?.Value;

            // Fall back to email claim
            if (string.IsNullOrWhiteSpace(username))
            {
                username = user.FindFirst(c => c.Type == "email")?.Value;
            }

            // Fall back to name claim
            if (string.IsNullOrWhiteSpace(username))
            {
                username = user.FindFirst(c => c.Type == "name")?.Value;
            }

            // Fall back to identity name
            if (string.IsNullOrWhiteSpace(username))
            {
                username = user.Identity.Name;
            }

            return username ?? string.Empty;
        }

        return string.Empty;
    }

    public async Task Login()
    {
        var scopes = new string[] { $"api://{_appSettings.AzureAd.ClientId}/{_appSettings.AzureAd.DefaultScope}" };

        try
        {
            var token = await _msalService.AcquireTokenAsync(prompt: LoginPrompt.Login, scopes: scopes);

            if (token == null)
            {
                _logger.LogWarning("Login failed: Token acquisition returned null");
                return;
            }

            if (_authenticationStateProvider is ApiAuthenticationStateProvider apiProvider)
            {
                await apiProvider.MarkUserAsAuthenticated(token.AccessToken);
            }

            _logger.LogInformation("User logged in successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Login error");
        }
    }

    public Task Logout()
    {
        if (_authenticationStateProvider is ApiAuthenticationStateProvider apiProvider)
        {
            apiProvider.MarkUserAsLoggedOut();
        }

        _logger.LogInformation("User logged out");

        return Task.CompletedTask;
    }
}
