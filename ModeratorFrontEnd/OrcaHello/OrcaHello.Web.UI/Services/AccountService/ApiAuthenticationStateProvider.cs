namespace OrcaHello.Web.UI.Services
{
    /// <summary>
    /// Custom authentication state provider for interacting with Azure AD and managing the authentication token.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public class ApiAuthenticationStateProvider : AuthenticationStateProvider
    {
        private readonly HttpClient _httpClient;
        private readonly IMemoryCache _memoryCache;
        private readonly IJSRuntime _jsRuntime;
        private readonly ClaimsPrincipal _anonymous = new(new ClaimsIdentity());
        private const string _tokenName = "authToken";
        private const string _headerName = "bearer";
        private const string _claimIdentity = "jwt";

        public ApiAuthenticationStateProvider(HttpClient httpClient, IMemoryCache memoryCache, IJSRuntime jsRuntime)
        {
            _httpClient = httpClient;
            _memoryCache = memoryCache;
            _jsRuntime = jsRuntime;
        }

        public override async Task<AuthenticationState> GetAuthenticationStateAsync()
        {
            var savedToken = await GetToken();

            if (string.IsNullOrWhiteSpace(savedToken))
            {
                return new AuthenticationState(_anonymous);
            }

            _httpClient.DefaultRequestHeaders.Authorization =
                new AuthenticationHeaderValue(_headerName, savedToken);

            return new AuthenticationState(new ClaimsPrincipal(new ClaimsIdentity(
                    ParseClaimsFromJwt(savedToken), _claimIdentity)));
        }

        public async Task<string> GetToken()
        {
            var memoryToken = _memoryCache.Get<string>(_tokenName);

            if (!string.IsNullOrWhiteSpace(memoryToken) && !IsTokenExpired(memoryToken))
            {
                return memoryToken;
            }

            if (await IsAppActiveAsync())
            {
                var localToken = await _jsRuntime.InvokeAsync<string>("localStorage.getItem", _tokenName);

                if (!string.IsNullOrWhiteSpace(localToken) && !IsTokenExpired(localToken))
                {
                    _memoryCache.Set<string>(_tokenName, localToken);
                    return localToken;
                }
            }

            return string.Empty;
        }

        public async Task SetToken(string token)
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.setItem", _tokenName, token);

            _memoryCache.Set(_tokenName, token);
        }

        public async Task ClearToken()
        {
            await _jsRuntime.InvokeVoidAsync("localStorage.removeItem", _tokenName);

            _memoryCache.Remove(_tokenName);
        }

        public void ClearMemoryToken()
        {
            _memoryCache.Remove(_tokenName);
        }

        private async Task<bool> IsAppActiveAsync()
        {
            bool isRunning = await _jsRuntime.InvokeAsync<bool>("eval", "window.location.href != 'about:blank'");
            return isRunning;
        }

        public async Task MarkUserAsAuthenticated()
        {
            var authState = Task.FromResult(await GetAuthenticationStateAsync());
            NotifyAuthenticationStateChanged(authState);
        }

        public void MarkUserAsLoggedOut()
        {
            var authState = Task.FromResult(new AuthenticationState(_anonymous));
            NotifyAuthenticationStateChanged(authState);
        }

        public bool IsTokenExpired(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return true;
            }

            // Parse the JWT and get the expiry time
            var jwtHandler = new JwtSecurityTokenHandler();
            var jwtSecurityToken = jwtHandler.ReadJwtToken(token);
            var expiryTime = jwtSecurityToken.ValidTo;

            // Check if the JWT has expired
            if (expiryTime < DateTime.UtcNow)
            {
                return true;
            }

            return false;
        }

        private IEnumerable<Claim> ParseClaimsFromJwt(string jwt)
        {
            var claims = new List<Claim>();
            var payload = jwt.Split('.')[1];
            var jsonBytes = ParseBase64WithoutPadding(payload);
            var keyValuePairs = JsonSerializer.Deserialize<Dictionary<string, object>>(jsonBytes);

            if (keyValuePairs is not null)
            {
                keyValuePairs.TryGetValue("groups", out object? groups);

                if (groups is not null)
                {
                    if (groups.ToString()!.Trim().StartsWith("["))
                    {
                        var parsedGroups = JsonSerializer.Deserialize<string[]>(groups.ToString()!);

                        if (parsedGroups is not null)
                        {
                            foreach (var parsedGroup in parsedGroups)
                            {
                                claims.Add(new Claim("groups", parsedGroup));
                            }
                        }
                    }
                    else
                    {
                        claims.Add(new Claim("groups", groups.ToString()!));
                    }

                    keyValuePairs.Remove("groups");
                }

                keyValuePairs.TryGetValue(ClaimTypes.Role, out object? roles);

                if (roles is not null)
                {
                    if (roles.ToString()!.Trim().StartsWith("["))
                    {
                        var parsedRoles = JsonSerializer.Deserialize<string[]>(roles.ToString()!);

                        if (parsedRoles is not null)
                        {
                            foreach (var parsedRole in parsedRoles)
                            {
                                claims.Add(new Claim(ClaimTypes.Role, parsedRole));
                            }
                        }
                    }
                    else
                    {
                        claims.Add(new Claim(ClaimTypes.Role, roles.ToString()!));
                    }

                    keyValuePairs.Remove(ClaimTypes.Role);
                }

                claims.AddRange(keyValuePairs.Select(kvp => new Claim(kvp.Key, kvp.Value.ToString()!)));
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
}
