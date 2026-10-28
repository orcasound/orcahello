using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Net.Http;
using System.Threading.Tasks;

namespace NotificationSystem.Utilities
{
    public class FacebookPostService : IFacebookPostService
    {
        private readonly HttpClient _httpClient;
        private readonly IConfiguration _configuration;
        private readonly ILogger<FacebookPostService> _logger;

        public FacebookPostService(
            HttpClient httpClient,
            IConfiguration configuration,
            ILogger<FacebookPostService> logger)
        {
            _httpClient = httpClient;
            _configuration = configuration;
            _logger = logger;
        }

        public async Task<bool> PostToPageAsync(string message)
        {
            string pageId = _configuration["FB_PAGE_ID"];
            string pageAccessToken = _configuration["FB_PAGE_ACCESS_TOKEN"];
            string apiVersion = _configuration["FB_GRAPH_API_VERSION"] ?? "v21.0";

            if (string.IsNullOrWhiteSpace(pageId))
            {
                _logger.LogError("Missing required configuration: FB_PAGE_ID");
                return false;
            }

            if (string.IsNullOrWhiteSpace(pageAccessToken))
            {
                _logger.LogError("Missing required configuration: FB_PAGE_ACCESS_TOKEN");
                return false;
            }

            string uri = $"https://graph.facebook.com/{apiVersion}/{pageId}/feed";
            using var content = new FormUrlEncodedContent(new Dictionary<string, string>
            {
                ["message"] = message,
                ["access_token"] = pageAccessToken
            });

            using HttpResponseMessage response = await _httpClient.PostAsync(uri, content);
            if (!response.IsSuccessStatusCode)
            {
                string responseBody = await response.Content.ReadAsStringAsync();
                _logger.LogError(
                    "Facebook post failed. StatusCode: {StatusCode}. Response: {ResponseBody}",
                    response.StatusCode,
                    responseBody);
                return false;
            }

            _logger.LogInformation("Facebook post created successfully.");
            return true;
        }
    }
}
