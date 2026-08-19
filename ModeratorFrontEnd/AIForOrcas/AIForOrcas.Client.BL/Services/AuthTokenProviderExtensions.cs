using System.Net.Http;
using System.Net.Http.Headers;

namespace AIForOrcas.Client.BL.Services
{
    public static class AuthTokenProviderExtensions
    {
        public static void ApplyToken(this IAuthTokenProvider provider, HttpRequestMessage request)
        {
            var token = provider.GetToken();
            if (!string.IsNullOrWhiteSpace(token))
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            }
        }
    }
}
