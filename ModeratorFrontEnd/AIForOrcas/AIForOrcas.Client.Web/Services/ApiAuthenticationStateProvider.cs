using Microsoft.Extensions.Logging;

namespace AIForOrcas.Client.Web.Services;

public class ApiAuthenticationStateProvider : AuthenticationStateProvider
{
    private readonly ITokenStore _tokenStore;
    private readonly CircuitHandlerService _circuitHandler;
    private readonly ILogger<ApiAuthenticationStateProvider> _logger;
    private ClaimsPrincipal _currentUser = new ClaimsPrincipal(new ClaimsIdentity());

    private static int _instanceCount = 0;
    private readonly int _instanceId;

    public ApiAuthenticationStateProvider(
        ITokenStore tokenStore,
        CircuitHandlerService circuitHandler,
        ILogger<ApiAuthenticationStateProvider> logger)
    {
        _tokenStore = tokenStore;
        _circuitHandler = circuitHandler;
        _logger = logger;

        _instanceId = Interlocked.Increment(ref _instanceCount);
        _logger.LogDebug("ApiAuthenticationStateProvider Instance #{InstanceId} created", _instanceId);
    }

    public override Task<AuthenticationState> GetAuthenticationStateAsync()
    {
        _logger.LogDebug("GetAuthenticationStateAsync called on Instance #{InstanceId}", _instanceId);

        var circuitId = _circuitHandler.CircuitId;
        if (!string.IsNullOrWhiteSpace(circuitId))
        {
            var token = _tokenStore.GetToken(circuitId);
            if (!string.IsNullOrWhiteSpace(token))
            {
                // Reconstruct user from token.
                try
                {
                    var claims = ParseClaimsFromJwt(token).ToList();
                    _currentUser = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));

                    _logger.LogDebug("Instance #{InstanceId} - User authenticated from token store", _instanceId);
                }
                catch (Exception ex)
                {
                    _tokenStore.RemoveToken(circuitId);
                    _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
                    _logger.LogError(ex, "Error parsing token from store; token cleared");
                }
            }
            else
            {
                _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
                _logger.LogDebug("Instance #{InstanceId} - No token in store", _instanceId);
            }
        }
        else
        {
            _currentUser = new ClaimsPrincipal(new ClaimsIdentity());
            _logger.LogWarning("Instance #{InstanceId} has no circuit ID; treating user as anonymous", _instanceId);
        }

        return Task.FromResult(new AuthenticationState(_currentUser));
    }

    public Task MarkUserAsAuthenticated(string token)
    {
        _logger.LogDebug("MarkUserAsAuthenticated called on Instance #{InstanceId}", _instanceId);

        if (string.IsNullOrWhiteSpace(token))
        {
            _logger.LogWarning("Attempted to mark user as authenticated with empty token");
            return Task.CompletedTask;
        }

        try
        {
            // Store token in server-side store (SINGLETON - shared across instances)
            var circuitId = _circuitHandler.CircuitId;
            if (string.IsNullOrWhiteSpace(circuitId))
            {
                _logger.LogWarning("Instance #{InstanceId} has no circuit ID; cannot mark user authenticated", _instanceId);
                return Task.CompletedTask;
            }
            _tokenStore.SetToken(circuitId, token);

            // Parse claims and update local state.
            var claims = ParseClaimsFromJwt(token).ToList();

            _currentUser = new ClaimsPrincipal(new ClaimsIdentity(claims, "jwt"));

            _logger.LogDebug("Instance #{InstanceId} - _currentUser.IsAuthenticated = {IsAuth}",
                _instanceId, _currentUser.Identity?.IsAuthenticated ?? false);

            // Notify authentication state changed.
            NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));

            _logger.LogInformation("User authenticated successfully");
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error marking user as authenticated");
        }

        return Task.CompletedTask;
    }

    public void MarkUserAsLoggedOut()
    {
        _logger.LogDebug("MarkUserAsLoggedOut called on Instance #{InstanceId}", _instanceId);

        var circuitId = _circuitHandler.CircuitId;
        if (!string.IsNullOrWhiteSpace(circuitId))
        {
            _tokenStore.RemoveToken(circuitId);
        }

        _currentUser = new ClaimsPrincipal(new ClaimsIdentity());

        NotifyAuthenticationStateChanged(Task.FromResult(new AuthenticationState(_currentUser)));

        _logger.LogInformation("User logged out");
    }

    public string GetToken()
    {
        var circuitId = _circuitHandler.CircuitId;
        if (string.IsNullOrWhiteSpace(circuitId))
        {
            return null;
        }

        return _tokenStore.GetToken(circuitId);
    }

    private IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
    {
        var claims = new List<Claim>();

        try
        {
            var payload = jwt.Split('.')[1];
            var jsonBytes = ParseBase64WithoutPadding(payload);
            var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);

            if (keyValuePairs == null) return claims;

            // Handle groups.
            if (keyValuePairs.TryGetValue("groups", out object groups) && groups != null)
            {
                if (groups.ToString().Trim().StartsWith("["))
                {
                    var parsedGroups = JsonSerializer.Deserialize<string[]>(groups.ToString());
                    if (parsedGroups != null)
                    {
                        foreach (var parsedGroup in parsedGroups)
                        {
                            claims.Add(new Claim("groups", parsedGroup));
                        }
                    }
                }
                else
                {
                    claims.Add(new Claim("groups", groups.ToString()));
                }
                keyValuePairs.Remove("groups");
            }

            // Handle roles.
            if (keyValuePairs.TryGetValue(ClaimTypes.Role, out object roles) && roles != null)
            {
                if (roles.ToString().Trim().StartsWith("["))
                {
                    var parsedRoles = JsonSerializer.Deserialize<string[]>(roles.ToString());
                    if (parsedRoles != null)
                    {
                        foreach (var parsedRole in parsedRoles)
                        {
                            claims.Add(new Claim(ClaimTypes.Role, parsedRole));
                        }
                    }
                }
                else
                {
                    claims.Add(new Claim(ClaimTypes.Role, roles.ToString()));
                }
                keyValuePairs.Remove(ClaimTypes.Role);
            }

            claims.AddRange(keyValuePairs.Select(kvp => new Claim(kvp.Key, kvp.Value?.ToString() ?? string.Empty)));
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error parsing claims from JWT");
        }

        return claims;
    }

    private byte[] ParseBase64WithoutPadding(string base64)
    {
        switch (base64.Length % 4)
        {
            case 2: base64 += "=="; break;
            case 3: base64 += "="; break;
        }
        return Convert.FromBase64String(base64);
    }
}
