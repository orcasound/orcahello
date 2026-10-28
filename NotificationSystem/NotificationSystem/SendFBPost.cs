using ComposableAsync;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using NotificationSystem.Template;
using NotificationSystem.Utilities;
using RateLimiter;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Text.Json;
using System.Threading.Tasks;

namespace NotificationSystem
{
    public class SendFBPost
    {
        private readonly ILogger _logger;
        private readonly IConfiguration _configuration;
        private readonly IFacebookPostService _facebookPostService;
        private const int SendRate = 5;

        public SendFBPost(
            ILogger<SendFBPost> logger,
            IConfiguration configuration,
            IFacebookPostService facebookPostService)
        {
            _logger = logger;
            _configuration = configuration;
            _facebookPostService = facebookPostService;
        }

        public async Task ProcessDocumentsAsync(IReadOnlyList<JsonElement> input)
        {
            if (input == null || input.Count == 0)
            {
                _logger.LogInformation("No updated records");
                return;
            }

            var timeConstraint = TimeLimiter.GetFromMaxCountByInterval(SendRate, TimeSpan.FromSeconds(1));
            foreach (var document in input)
            {
                if (!TryBuildPostMessage(document, out string message))
                {
                    continue;
                }

                await timeConstraint;
                bool posted = await _facebookPostService.PostToPageAsync(message);
                if (!posted)
                {
                    _logger.LogError("Failed to create Facebook post.");
                }
            }
        }

        [Function("SendFBPost")]
        public async Task Run(
            [CosmosDBTrigger(
                databaseName: "predictions",
                containerName: "metadata",
                Connection = "aifororcasmetadatastore_DOCUMENTDB",
                LeaseContainerName = "leases",
                LeaseContainerPrefix = "facebook",
                CreateLeaseContainerIfNotExists = true)] IReadOnlyList<JsonElement> input)
        {
            if (!IsEnabled())
            {
                _logger.LogInformation("SendFBPost is disabled via FB_POST_ENABLED.");
                return;
            }

            await ProcessDocumentsAsync(input);
        }

        private bool IsEnabled()
        {
            return bool.TryParse(_configuration["FB_POST_ENABLED"], out bool enabled) && enabled;
        }

        private bool TryBuildPostMessage(JsonElement document, out string message)
        {
            message = null;

            bool isReviewed = document.TryGetProperty("reviewed", out JsonElement reviewed) &&
                reviewed.ValueKind == JsonValueKind.True;
            bool isConfirmedSrkw = document.TryGetProperty("SRKWFound", out JsonElement srkwFound) &&
                srkwFound.ValueKind == JsonValueKind.String &&
                string.Equals(srkwFound.GetString(), "yes", StringComparison.OrdinalIgnoreCase);

            if (isReviewed && isConfirmedSrkw)
            {
                DateTime? timestamp = null;
                if (document.TryGetProperty("timestamp", out var timestampElement))
                {
                    timestamp = timestampElement.GetDateTime();
                }

                string location = "Unknown";
                if (document.TryGetProperty("location", out var locationElement) &&
                    locationElement.TryGetProperty("name", out var nameElement))
                {
                    location = nameElement.GetString() ?? "Unknown";
                }

                string comments = document.TryGetProperty("comments", out var commentsElement) &&
                    commentsElement.ValueKind == JsonValueKind.String
                    ? commentsElement.GetString()
                    : null;

                string category = EmailTemplate.GetCategory(comments);
                message = CreatePostMessage(timestamp, category, location);
                return true;
            }

            _logger.LogInformation("Skipping record that is not moderator-approved SRKW confirmation.");
            return false;
        }

        internal static string CreatePostMessage(DateTime? timestamp, string category, string location)
        {
            string timeString = timestamp.HasValue
                ? timestamp.Value.ToString("yyyy-MM-dd HH:mm:ss 'UTC'", CultureInfo.InvariantCulture)
                : "unknown time";

            return
                $"{category} call confirmed by a moderator.\n" +
                $"Time (UTC): {timeString}\n" +
                $"Location: {location}\n\n" +
                "View live detections in the Orca moderation portal:\n" +
                "https://aifororcas.azurewebsites.net/";
        }
    }
}