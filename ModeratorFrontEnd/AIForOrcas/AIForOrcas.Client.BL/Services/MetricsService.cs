using AIForOrcas.DTO;
using AIForOrcas.DTO.API;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace AIForOrcas.Client.BL.Services
{
    public class MetricsService : IMetricsService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private string api = "api/metrics";
        private JsonSerializerOptions defaultJsonSerializerOptions => new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };

        public MetricsService(IHttpClientFactory httpClientFactory)
        {
            _httpClientFactory = httpClientFactory;
        }

        public async Task<ModeratorMetrics> GetModeratorMetricsAsync(IFilterOptions filterOptions)
        {
            var prefix = api.Contains("?") ? $"{api}/moderator&" : $"{api}/moderator?";
            var url = $"{prefix}{filterOptions.QueryString}";

            var httpClient = _httpClientFactory.CreateClient("UnauthenticatedAPI");
            var httpResponseMessage = await httpClient.GetAsync(url);

            if (httpResponseMessage.IsSuccessStatusCode)
            {
                var responseString = await httpResponseMessage.Content.ReadAsStringAsync();

                if (httpResponseMessage.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    return new ModeratorMetrics() { HasContent = false };
                }

                var response = JsonSerializer.Deserialize<ModeratorMetrics>(responseString, defaultJsonSerializerOptions);
                response.HasContent = true;

                return response;
            }
            else
            {
                return new ModeratorMetrics() { HasContent = false };
            }
        }

        public async Task<Metrics> GetSiteMetricsAsync(IFilterOptions filterOptions)
        {
            var prefix = api.Contains("?") ? $"{api}/system&" : $"{api}/system?";
            var url = $"{prefix}{filterOptions.QueryString}";

            var httpClient = _httpClientFactory.CreateClient("UnauthenticatedAPI");
            var httpResponseMessage = await httpClient.GetAsync(url);

            if (httpResponseMessage.IsSuccessStatusCode)
            {
                var responseString = await httpResponseMessage.Content.ReadAsStringAsync();

                if (httpResponseMessage.StatusCode == System.Net.HttpStatusCode.NoContent)
                {
                    return new Metrics() { HasContent = false };
                }

                var response = JsonSerializer.Deserialize<Metrics>(responseString, defaultJsonSerializerOptions);
                response.HasContent = true;

                return response;
            }
            else
            {
                return new Metrics() { HasContent = false };
            }
        }
    }
}
