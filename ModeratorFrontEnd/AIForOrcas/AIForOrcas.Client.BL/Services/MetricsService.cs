using AIForOrcas.DTO;
using AIForOrcas.DTO.API;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;
using Microsoft.Extensions.Logging;


namespace AIForOrcas.Client.BL.Services
{
    public class MetricsService : IMetricsService
    {
        private readonly ILogger<MetricsService> _logger;
        private readonly IApiClientHelper _apiClientHelper;
        private string api = "api/metrics";

        public MetricsService(ILogger<MetricsService> logger, IApiClientHelper apiClientHelper)
        {
            _logger = logger;
            _apiClientHelper = apiClientHelper ?? throw new System.ArgumentNullException(nameof(apiClientHelper));
        }

        public async Task<ModeratorMetrics> GetModeratorMetricsAsync(IFilterOptions filterOptions)
        {
            var prefix = api.Contains("?") ? $"{api}/moderator&" : $"{api}/moderator?";
            var url = $"{prefix}{filterOptions.QueryString}";

            var (value, response) = await _apiClientHelper.GetJsonAsync<ModeratorMetrics>("UnauthenticatedAPI", url);

            if (response == null)
            {
                return new ModeratorMetrics() { HasContent = false };
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                return new ModeratorMetrics() { HasContent = false };
            }

            if (response.IsSuccessStatusCode)
            {
                if (value == null)
                {
                    return new ModeratorMetrics() { HasContent = false };
                }

                value.HasContent = true;
                return value;
            }

            return new ModeratorMetrics() { HasContent = false };
        }

        public async Task<Metrics> GetSiteMetricsAsync(IFilterOptions filterOptions)
        {
            var prefix = api.Contains("?") ? $"{api}/system&" : $"{api}/system?";
            var url = $"{prefix}{filterOptions.QueryString}";

            var (value, response) = await _apiClientHelper.GetJsonAsync<Metrics>("UnauthenticatedAPI", url);

            if (response == null)
            {
                return new Metrics() { HasContent = false };
            }

            if (response.StatusCode == System.Net.HttpStatusCode.NoContent)
            {
                return new Metrics() { HasContent = false };
            }

            if (response.IsSuccessStatusCode)
            {
                if (value == null)
                {
                    return new Metrics() { HasContent = false };
                }

                value.HasContent = true;
                return value;
            }

            return new Metrics() { HasContent = false };
        }
    }
}
