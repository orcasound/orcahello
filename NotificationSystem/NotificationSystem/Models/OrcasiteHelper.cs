using AIForOrcas.DTO;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Net.Http.Headers;
using System.Text.Json;
using System.Text.Json.Nodes;
using System.Threading.Tasks;

namespace NotificationSystem.Models
{
    public class OrcasiteHelper
    {
        private readonly HttpClient _httpClient;
        private string _orcasiteHostname;
        private string _orcasiteApiKey;
        private JsonElement? _orcasiteFeedsArray;
        private readonly ILogger<OrcasiteHelper> _logger;
        private DateTime? _currentEpochStart;
        private bool _initialized = false;

        public OrcasiteHelper(ILogger<OrcasiteHelper> log, HttpClient httpClient)
        {
            _logger = log;
            _httpClient = httpClient;
        }

        /// <summary>
        /// URI of Orcasite API to post a machine detection.
        /// See https://live.orcasound.net/api/json/swaggerui for details.
        /// </summary>
        public string OrcasitePostDetectionUri
        {
            get
            {
                return "https://" + _orcasiteHostname + "/api/json/detections?fields%5Bdetection%5D=id%2Csource_ip%2Cplaylist_timestamp%2Cplayer_offset%2Clistener_count%2Ctimestamp%2Cdescription%2Cvisible%2Csource%2Ccategory%2Ccandidate_id%2Cfeed_id";
            }
        }

        /// <summary>
        /// URI of Orcasite API to query feeds metadata.
        /// See https://live.orcasound.net/api/json/swaggerui for details.
        /// </summary>
        public string OrcasiteGetFeedsUri
        {
            get
            {
                return "https://" + _orcasiteHostname + "/api/json/feeds?fields%5Bfeed%5D=id%2Cname%2Cnode_name%2Cslug%2Clocation_point%2Cintro_html%2Cimage_url%2Cvisible%2Cbucket%2Cbucket_region%2Ccloudfront_url%2Cdataplicity_id%2Corcahello_id";
            }
        }

        /// <summary>
        /// Get an array of data from a JSON API endpoint.
        /// </summary>
        /// <param name="uri">URI to fetch</param>
        /// <returns>JsonElement representing the JSON array</returns>
        private async Task<JsonElement?> GetDataArrayAsync(string uri)
        {
            try
            {
                string jsonString = await _httpClient.GetStringAsync(uri);
                JsonElement objectElement = JsonSerializer.Deserialize<JsonElement>(jsonString);
                if (objectElement.ValueKind != JsonValueKind.Object)
                {
                    _logger.LogError($"Invalid objectElement kind in ExecuteTask: {objectElement.ValueKind}");
                    return null;
                }
                if (!objectElement.TryGetProperty("data", out var arrayElement))
                {
                    _logger.LogError($"Missing data in ExecuteTask result");
                    return null;
                }
                if (arrayElement.ValueKind != JsonValueKind.Array)
                {
                    _logger.LogError($"Invalid arrayElement kind in ExecuteTask: {arrayElement.ValueKind}");
                    return null;
                }
                return arrayElement;
            }
            catch (Exception e)
            {
                _logger.LogError($"Error: {e.Message}");
                return null;
            }
        }

        /// <summary>
        /// Initialize the OrcasiteHelper object.  Currently this entails
        /// fetching the feeds metadata from Orcasite so that we can convert
        /// a node name to a feed ID.
        ///
        /// TODO: In the future, we could store this info in OrcaHello, or
        /// make the Orcasite API accept OrcaHello feed IDs, and avoid this
        /// extra query.
        /// </summary>
        /// <returns>A task that represents the asynchronous operation</returns>
        public async Task InitializeAsync(IConfiguration configuration)
        {
            if (_initialized)
            {
                return;
            }
            if (_orcasiteFeedsArray == null)
            {
                _orcasiteApiKey = configuration["ORCASITE_APIKEY"];
                _orcasiteHostname = configuration["ORCASITE_HOSTNAME"] ?? "live.orcasound.net";
                _orcasiteFeedsArray = await GetDataArrayAsync(OrcasiteGetFeedsUri);
                if (_orcasiteFeedsArray == null)
                {
                    _logger.LogError("Failed to retrieve orcasite feeds.");
                }
                else
                {
                    HydrophoneLocations.Initialize(GetLocationToHydrophoneIdMap(_orcasiteFeedsArray.Value));
                }

                string? currentEpochStart = configuration["CURRENT_EPOCH_START"];
                if (currentEpochStart != null)
                {
                    if (DateTime.TryParse(currentEpochStart, out DateTime parsedDate))
                    {
                        _currentEpochStart = parsedDate;
                    }
                }
            }
            _initialized = true;
        }

        private static IReadOnlyDictionary<string, string> GetLocationToHydrophoneIdMap(JsonElement feedsArray)
        {
            var locationToIdMap = new Dictionary<string, string>();

            foreach (JsonElement feed in feedsArray.EnumerateArray())
            {
                if (!feed.TryGetProperty("attributes", out var attributes) || attributes.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }

                if (!attributes.TryGetProperty("name", out var locationNameElement) || locationNameElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                if (!attributes.TryGetProperty("node_name", out var hydrophoneIdElement) || hydrophoneIdElement.ValueKind != JsonValueKind.String)
                {
                    continue;
                }

                string locationName = locationNameElement.GetString();
                string hydrophoneId = hydrophoneIdElement.GetString();
                if (string.IsNullOrWhiteSpace(locationName) || string.IsNullOrWhiteSpace(hydrophoneId))
                {
                    continue;
                }

                locationToIdMap[locationName] = hydrophoneId;
            }

            return locationToIdMap;
        }

        /// <summary>
        /// Convert a node name to a feed ID.
        /// </summary>
        /// <param name="nodeNameToFind">Node name to find</param>
        /// <param name="feedsArray">Feeds array to look in</param>
        /// <returns>Feed ID, or null if not found</returns>
        private string GetFeedId(string nodeNameToFind, JsonElement feedsArray)
        {
            foreach (JsonElement feed in feedsArray.EnumerateArray())
            {
                if (!feed.TryGetProperty("attributes", out var attributes))
                {
                    _logger.LogError($"Missing attributes in ExecuteTask result");
                    continue;
                }
                if (attributes.ValueKind != JsonValueKind.Object)
                {
                    _logger.LogError($"Invalid attributes kind in ExecuteTask: {attributes.ValueKind}");
                    continue;
                }
                if (!attributes.TryGetProperty("node_name", out var nodeName))
                {
                    _logger.LogError($"Missing node_name in ExecuteTask result");
                    continue;
                }
                if (nodeName.ValueKind != JsonValueKind.String)
                {
                    _logger.LogError($"Invalid node_name kind in ExecuteTask: {nodeName.ValueKind}");
                    continue;
                }
                string nodeNameString = nodeName.GetString();
                if (nodeNameString != nodeNameToFind)
                {
                    continue;
                }

                if (!feed.TryGetProperty("id", out var feedId))
                {
                    _logger.LogError($"Missing id in ExecuteTask result");
                    continue;
                }
                if (feedId.ValueKind != JsonValueKind.String)
                {
                    _logger.LogError($"Invalid id kind in ExecuteTask: {nodeName.ValueKind}");
                    continue;
                }

                return feedId.GetString();
            }
            return null;
        }

        /// <summary>
        /// Get the slug for a location by searching the feeds array by location name.
        /// </summary>
        /// <param name="locationName">Location name to find (e.g., "Bush Point", "North San Juan Channel")</param>
        /// <returns>Slug string (e.g., "bush-point", "north-sjc"), or null if not found or feeds not initialized</returns>
        public virtual string GetSlugByLocationName(string locationName)
        {
            if (_orcasiteFeedsArray == null)
            {
                _logger.LogWarning($"Feeds array not initialized when looking up slug for: {locationName}");
                return null;
            }

            foreach (JsonElement feed in _orcasiteFeedsArray.Value.EnumerateArray())
            {
                if (!feed.TryGetProperty("attributes", out var attributes))
                {
                    continue;
                }
                if (attributes.ValueKind != JsonValueKind.Object)
                {
                    continue;
                }
                if (!attributes.TryGetProperty("name", out var name))
                {
                    continue;
                }
                if (name.ValueKind != JsonValueKind.String)
                {
                    continue;
                }
                string feedName = name.GetString();
                
                // Match location name (case-insensitive)
                if (string.Equals(feedName, locationName, StringComparison.OrdinalIgnoreCase) || 
                    feedName.Contains(locationName, StringComparison.OrdinalIgnoreCase))
                {
                    if (attributes.TryGetProperty("slug", out var slug) && slug.ValueKind == JsonValueKind.String)
                    {
                        return slug.GetString();
                    }
                }
            }
            
            _logger.LogWarning($"Could not find slug for location: {locationName}");
            return null;
        }

        /// <summary>
        /// Given a detection, extract the nominal timestamp from it.
        /// </summary>
        /// <param name="orcaHelloDetection">Detection to look in</param>
        /// <returns>Timestamp in DateTime format, or null on error</returns>
        DateTime? TryGetTimestamp(JsonElement orcaHelloDetection)
        {
            if (!orcaHelloDetection.TryGetProperty("timestamp", out var timestamp))
            {
                _logger.LogError($"Missing timestamp in ExecuteTask result");
                return null;
            }
            if (timestamp.ValueKind != JsonValueKind.String)
            {
                _logger.LogError($"Invalid timestamp kind in ExecuteTask: {timestamp.ValueKind}");
                return null;
            }
            string timestampString = timestamp.GetString();
            if (!DateTime.TryParse(timestampString, out DateTime dateTime))
            {
                _logger.LogError($"Invalid timestamp ExecuteTask: {timestamp}");
                return null;
            }
            return dateTime;
        }

        /// <summary>
        /// Given a detection, extract the location id (e.g., "rpi_orcasound_lab") from it.
        /// </summary>
        /// <param name="orcaHelloDetection">Detection to look in</param>
        /// <returns>Location ID as used by S3 and Orcasite</returns>
        string TryGetLocationIdString(JsonElement orcaHelloDetection)
        {
            if (!orcaHelloDetection.TryGetProperty("location", out var location))
            {
                _logger.LogError($"Missing location in ExecuteTask result");
                return null;
            }
            if (location.ValueKind != JsonValueKind.Object)
            {
                _logger.LogError($"Invalid location kind in ExecuteTask: {location.ValueKind}");
                return null;
            }

            if (!location.TryGetProperty("id", out var locationId))
            {
                _logger.LogError($"Missing location.id in ExecuteTask result");
                return null;
            }
            if (locationId.ValueKind != JsonValueKind.String)
            {
                _logger.LogError($"Invalid location.id kind in ExecuteTask: {locationId.ValueKind}");
                return null;
            }
            string locationIdString = locationId.GetString();
            if (locationIdString == null)
            {
                _logger.LogError($"Couldn't get location ID as a string");
                return null;
            }
            return locationIdString;
        }

        /// <summary>
        /// Get the start time offset, in seconds, of the best prediction.
        /// </summary>
        /// <param name="orcaHelloDetection">OrcaHello detection to check</param>
        /// <returns>Start time offset, in seconds, or null on error</returns>
        double? TryGetBestStartTime(JsonElement orcaHelloDetection)
        {
            if (!orcaHelloDetection.TryGetProperty("predictions", out var predictions))
            {
                _logger.LogError($"Missing predictions");
                return null;
            }
            if (predictions.ValueKind != JsonValueKind.Array)
            {
                _logger.LogError($"Invalid predictions kind in ExecuteTask: {predictions.ValueKind}");
                return null;
            }
            double bestConfidence = 0;
            double startTime = 0;
            foreach (JsonElement prediction in predictions.EnumerateArray())
            {
                if (!prediction.TryGetProperty("confidence", out var confidenceElement))
                {
                    _logger.LogError($"Missing confidence");
                    return null;
                }
                if (!confidenceElement.TryGetDouble(out var confidence))
                {
                    _logger.LogError($"Invalid confidence in ExecuteTask: {confidenceElement.ValueKind}");
                    return null;
                }
                if (confidence <= bestConfidence)
                {
                    continue;
                }
                if (!prediction.TryGetProperty("startTime", out var startTimeElement))
                {
                    _logger.LogError($"Missing startTime");
                    return null;
                }
                if (!startTimeElement.TryGetDouble(out startTime))
                {
                    _logger.LogError($"Invalid startTime in ExecuteTask: {startTimeElement.ValueKind}");
                    return null;
                }
                bestConfidence = confidence;
            }
            if (bestConfidence == 0)
            {
                // No predictions found.
                return null;
            }
            return startTime;
        }

        /// <summary>
        /// Parse JSON representing an OrcaHello detection into just the fields we need.
        /// </summary>
        /// <param name="json">JSON to parse</param>
        /// <param name="id">ID of the detection</param>
        /// <param name="timestampString">Timestamp to report for the detection</param>
        /// <param name="feedId">Feed ID for the hydrophone node in the detection</param>
        /// <param name="commentsString">Comments in the detection</param>
        /// <returns>true on success, false on failure</returns>
        private bool ParseOrcaHelloDetection(string json, out string idString, out string timestampString, out string feedId, out string commentsString)
        {
            idString = null;
            timestampString = null;
            feedId = null;
            commentsString = null;

            JsonElement orcaHelloDetection = JsonDocument.Parse(json).RootElement;
            if (!orcaHelloDetection.TryGetProperty("id", out var id))
            {
                _logger.LogError($"Missing id in ExecuteTask result");
                return false;
            }
            if (id.ValueKind != JsonValueKind.String)
            {
                _logger.LogError($"Invalid id kind in ExecuteTask: {id.ValueKind}");
                return false;
            }
            idString = id.GetString();
            if (idString == null)
            {
                _logger.LogError($"Couldn't get ID as a string");
                return false;
            }

            string locationIdString = TryGetLocationIdString(orcaHelloDetection);
            if (locationIdString == null)
            {
                return false;
            }

            // Get feed ID from location ID.
            feedId = GetFeedId(locationIdString, _orcasiteFeedsArray.Value);
            if (feedId == null)
            {
                _logger.LogError($"Couldn't find feed id for: {locationIdString}");
                return false;
            }

            // Get timestamp according to OrcaHello.
            DateTime? dateTime = TryGetTimestamp(orcaHelloDetection);
            if (dateTime == null)
            {
                return false;
            }

            // Get offset of best prediction.
            double? startTime = TryGetBestStartTime(orcaHelloDetection);
            if (startTime != null)
            {
                // Adjust the timestamp by the start time of the best prediction.
                dateTime = dateTime.Value.AddSeconds(startTime.Value);
            }

            timestampString = dateTime.Value.ToString("o"); // ISO 8601 format

            // Get comments from OrcaHello.  This property will only be present
            // if the detection was already moderated within OrcaHello.
            if (orcaHelloDetection.TryGetProperty("comments", out var comments))
            {
                commentsString = comments.ValueKind == JsonValueKind.String ? comments.GetString() : null;
            }
            return true;
        }

        /// <summary>
        /// Given an OrcaHello detection (in JSON), report it to Orcasite.
        /// </summary>
        /// <param name="json">OrcaHello detection</param>
        /// <returns>true on success, false on failure</returns>
        public async Task<bool> PostDetectionAsync(string json)
        {
            if (!ParseOrcaHelloDetection(json, out var id, out var timestamp, out var feedId, out var comments))
            {
                return false;
            }

            // We assume that the Azure function lease mechanism ensures that each
            // detection is processed only once, so we don't need to check for duplicates.
            // We also assume that the Orcasite API will reject duplicates if they do occur.

            // Compose a detection post.
            JsonObject newDetection = new JsonObject
            {
                ["data"] = new JsonObject
                {
                    ["type"] = "detection",
                    ["attributes"] = new JsonObject
                    {
                        ["description"] = JsonValue.Create(comments),
                        ["feed_id"] = JsonValue.Create(feedId),
                        ["timestamp"] = JsonValue.Create(timestamp),
                        ["idempotency_key"] = JsonValue.Create(id),

                        // "source": "machine" is implied by a POST.
                        //
                        // "category" is currently disallowed by the API, even though
                        // it would make sense to add "category": "whale" to distinguish
                        // it from, say, a shipnoise detector.
                        //
                        // "playlist_timestamp" and "player_offset" are computed
                        // from the timestamp by the API service so are not needed here.
                    }
                }
            };

            // Try posting it.
            string newDetectionString = newDetection.ToJsonString(new JsonSerializerOptions { WriteIndented = true });
            HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, OrcasitePostDetectionUri)
            {
                Content = new StringContent(newDetectionString)
            };
            request.Content.Headers.ContentType = new MediaTypeHeaderValue("application/vnd.api+json");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/vnd.api+json"));
            if (_orcasiteApiKey != null)
            {
                request.Headers.Authorization = new AuthenticationHeaderValue("Bearer", _orcasiteApiKey);
            }

            HttpResponseMessage response = await _httpClient.SendAsync(request);
            if (!response.IsSuccessStatusCode)
            {
                string message = await response.Content.ReadAsStringAsync();
                _logger.LogError($"Error: {response.StatusCode} - {message}");
                return false;
            }

            _logger.LogInformation($"Detection for {timestamp} posted successfully!");
            return true;
        }

        Dictionary<string, List<string>> _s3FoldersCache = new Dictionary<string, List<string>>();

        /// <summary>
        /// Get the list of folders (representing .ts segment start times) for
        /// a given location ID from the public S3 bucket.
        /// </summary>
        /// <param name="locationIdString">Location ID to look in</param>
        /// <returns>List of folder names representing HLS start times</returns>
        public async Task<List<string>> GetPublicS3FoldersAsync(string locationIdString)
        {
            // First try using a cached value.
            if (_s3FoldersCache.TryGetValue(locationIdString, out var cachedFolders))
            {
                return cachedFolders;
            }

            var config = new AmazonS3Config
            {
                RegionEndpoint = RegionEndpoint.USWest2
            };

            using var client = new AmazonS3Client(new Amazon.Runtime.AnonymousAWSCredentials(), config);
            var allFolders = new List<string>();
            string continuationToken = null;

            do
            {
                var request = new ListObjectsV2Request
                {
                    BucketName = "audio-orcasound-net",
                    Prefix = locationIdString + "/hls/",
                    Delimiter = "/", // Group by folders
                    ContinuationToken = continuationToken
                };

                var response = await client.ListObjectsV2Async(request);

                var folderNames = response.CommonPrefixes
                    .Select(prefix => prefix.TrimEnd('/').Split('/').Last())
                    .ToList();

                allFolders.AddRange(folderNames);

                continuationToken = response.IsTruncated ?? false
                    ? response.NextContinuationToken
                    : null;
            } while (continuationToken != null);

            _s3FoldersCache[locationIdString] = allFolders;
            return allFolders;
        }

        /// <summary>
        /// Check the list of detections for those with incorrect timestamps and return an
        /// updated list with the corrected timestamps.  This can take several seconds since
        /// it requires querying S3 to find the .ts folders.  Hence, this should only be
        /// done for the old epoch, where it is known that the timestamps are incorrect.
        /// </summary>
        /// <param name="originalInput">List of detections to process</param>
        /// <returns>List of corrected detections</returns>
        public async Task<List<JsonElement>> FixTimestampsAsync(IReadOnlyList<JsonElement> originalInput)
        {
            var correctedInput = new List<JsonElement>();

            foreach (JsonElement originalDetection in originalInput)
            {
                // Get the timestamp that OrcaHello listed in the detection.
                // This is what was used to find the clips, but does not align
                // with the start of a clip.
                DateTime? originalDateTime = TryGetTimestamp(originalDetection);
                if (originalDateTime == null)
                {
                    continue;
                }

                // If the timestamp is in the current epoch, it is already correct.
                if ((_currentEpochStart != null) && (originalDateTime >= _currentEpochStart))
                {
                    correctedInput.Add(originalDetection.Clone());
                    continue;
                }

                // Convert dateTime to Unix time (seconds since 1970-01-01).
                var originalDateTimeOffset = new DateTimeOffset(originalDateTime.Value);
                long originalUnixTimeSeconds = originalDateTimeOffset.ToUnixTimeSeconds();

                // Fix the Unix time.  The originalDateTime is incorrect and
                // was computed based on clips being 11 seconds long instead
                // of 10 seconds long.  It is also based on the audio stream
                // starting as of the .ts folder date, instead of about 2 seconds
                // afterwards.  We can correct these once we know the
                // .ts folder date to start calculating the drift based on.

                string locationIdString = TryGetLocationIdString(originalDetection);
                if (string.IsNullOrEmpty(locationIdString))
                {
                    continue;
                }

                List<string> folders = await GetPublicS3FoldersAsync(locationIdString);

                // Find the most recent folder older than originalUnixTimeSeconds.
                long folderTimeSeconds = 0;
                foreach (var folderName in folders)
                {
                    if (long.TryParse(folderName, out long unixTime))
                    {
                        if (unixTime <= originalUnixTimeSeconds && unixTime > folderTimeSeconds)
                        {
                            folderTimeSeconds = unixTime;
                        }
                    }
                }
                if (folderTimeSeconds == 0)
                {
                    // No folder found.
                    continue;
                }

                // Compute the correct timestamp of the start of the 60 second clip.
                long originalSecondsIntoFolder = originalUnixTimeSeconds - folderTimeSeconds;
                long originalClipIndex = originalSecondsIntoFolder / 11;
                long correctedSecondsIntoFolder = originalClipIndex * 10 + 2;
                long correctedUnixTimeSeconds = folderTimeSeconds + correctedSecondsIntoFolder;
                DateTimeOffset correctedDateTimeOffset = DateTimeOffset.FromUnixTimeSeconds(correctedUnixTimeSeconds);
                string correctedTimestampString = correctedDateTimeOffset.ToUniversalTime()
                    .ToString("yyyy-MM-dd'T'HH:mm:ss.ffffff'Z'");

                JsonObject obj = JsonNode.Parse(originalDetection.GetRawText())!.AsObject();
                obj["timestamp"] = correctedTimestampString;

                string updatedJson = obj.ToJsonString();
                using JsonDocument doc = JsonDocument.Parse(updatedJson);
                correctedInput.Add(doc.RootElement.Clone());
            }
            return correctedInput;
        }
    }
}
