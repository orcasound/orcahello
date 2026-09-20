using AIForOrcas.DTO;
using AIForOrcas.DTO.API;
using Amazon;
using Amazon.S3;
using Amazon.S3.Model;
using Microsoft.Extensions.Logging;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text;
using System.Text.Json;
using System.Threading.Tasks;

namespace AIForOrcas.Client.BL.Services
{
    public class DetectionService : IDetectionService
    {
        private string api = "api/detections";
        private JsonSerializerOptions defaultJsonSerializerOptions => new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly IAuthTokenProvider _authTokenProvider;
        private readonly ILogger<DetectionService> _logger;

        /// <summary>
        /// Start of current timestamp epoch.  Any detection with a timestamp
        /// after this is assumed to be correct and does not need to be fixed.
        /// </summary>
        private readonly DateTime _currentEpochStart = new DateTime(2025, 10, 12, 14, 23, 00, DateTimeKind.Utc);

        public DetectionService(IHttpClientFactory httpClientFactory, IAuthTokenProvider authTokenProvider, ILogger<DetectionService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _authTokenProvider = authTokenProvider;
            _logger = logger;
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

            using (var client = new AmazonS3Client(new Amazon.Runtime.AnonymousAWSCredentials(), config))
            {
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
        }

        /// <summary>
        /// Check the list of detections for any with incorrect timestamps and
        /// update the list with corrected timestamps.
        /// </summary>
        /// <param name="detections">List of detections to process</param>
        public async Task FixTimestampsAsync(List<Detection> detections)
        {
            foreach (var detection in detections)
            {
                // Get the timestamp that OrcaHello listed in the detection.
                // This is what was used to find the clips, but does not align
                // with the start of a clip.
                DateTime? originalDateTime = detection.Timestamp;
                if (originalDateTime == null)
                {
                    continue;
                }

                // If the timestamp is in the current epoch, it is already correct.
                if ((_currentEpochStart != null) && (originalDateTime >= _currentEpochStart))
                {
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

                string locationIdString = detection.Location.Id;
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
                DateTime correctedDateTime = correctedDateTimeOffset.UtcDateTime;
                detection.Timestamp = correctedDateTime;
            }
        }


        // Get detections based on passed view, pagination options, and filter options
        private async Task<PaginatedResponseDTO<List<Detection>>> GetDetectionsAsync(string viewName, PaginationOptionsDTO paginationOptions, IFilterOptions filterOptions)
        {
            var prefix = api.Contains("?") ? $"{api}/{viewName}&" : $"{api}/{viewName}?";
            var url = $"{prefix}{paginationOptions.QueryString}&{filterOptions.QueryString}";

            // Create client on-demand from the current scope.
            var httpClient = _httpClientFactory.CreateClient("UnauthenticatedAPI");

            HttpResponseMessage httpResponseMessage;
            try
            {
                httpResponseMessage = await httpClient.GetAsync(url);
            }
            catch (Exception exception) when (exception is HttpRequestException || exception is TaskCanceledException)
            {
                // An unreachable or hung API must degrade like a failed status
                // code; an unhandled exception here would take down the whole
                // circuit.
                _logger.LogError(exception, "Unable to reach the detections API at {Url}", url);
                return new PaginatedResponseDTO<List<Detection>> { Response = null, TotalAmountPages = 0, TotalNumberRecords = 0, TotalNumberMinutes = 0 };
            }

            if (httpResponseMessage.IsSuccessStatusCode)
            {
                var responseString = await httpResponseMessage.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(responseString))
                {
                    return new PaginatedResponseDTO<List<Detection>> { Response = new List<Detection>(), TotalAmountPages = 0, TotalNumberRecords = 0, TotalNumberMinutes = 0 };
                }

                // The pagination headers are not guaranteed; a response without
                // them should not kill the page.
                httpResponseMessage.Headers.TryGetValues("totalAmountPages", out var pageValues);
                httpResponseMessage.Headers.TryGetValues("totalNumberRecords", out var recordValues);
                httpResponseMessage.Headers.TryGetValues("totalNumberMinutes", out var minuteValues);
                int.TryParse(pageValues?.FirstOrDefault(), out var totalAmountPages);
                int.TryParse(recordValues?.FirstOrDefault(), out var totalNumberRecords);
                int.TryParse(minuteValues?.FirstOrDefault(), out var totalNumberMinutes);

                try
                {
                    var detections = JsonSerializer.Deserialize<List<Detection>>(responseString, defaultJsonSerializerOptions);

                    // Attempt to fix timestamps, but do not let S3/network errors
                    // or other non-JSON exceptions break the caller. If fixing
                    // timestamps fails, return the original detections so the
                    // UI can still render the page instead of killing the
                    // Blazor circuit.
                    try
                    {
                        if (detections != null)
                        {
                            await FixTimestampsAsync(detections);
                        }
                    }
                    catch (Exception ex)
                    {
                        // Catch broad exceptions here intentionally; failures
                        // could be AmazonS3Exception, TaskCanceledException, etc.
                        _logger.LogWarning(ex, "Failed to correct timestamps for detections; returning unmodified detections. URL: {Url}", url);
                    }

                    return new PaginatedResponseDTO<List<Detection>>
                    {
                        Response = detections,
                        TotalAmountPages = totalAmountPages,
                        TotalNumberRecords = totalNumberRecords,
                        TotalNumberMinutes = totalNumberMinutes
                    };
                }
                catch (JsonException exception)
                {
                    _logger.LogError(exception, "Malformed response from the detections API at {Url}", url);
                    return new PaginatedResponseDTO<List<Detection>> { Response = null, TotalAmountPages = 0, TotalNumberRecords = 0, TotalNumberMinutes = 0 };
                }
            }
            else
            {
                return new PaginatedResponseDTO<List<Detection>> { Response = null, TotalAmountPages = 0, TotalNumberRecords = 0, TotalNumberMinutes = 0 };
            }
        }

        // Call the root GET (api/detections?...) so callers can request arbitrary date/location filtered sets.
        public async Task<PaginatedResponseDTO<List<Detection>>> GetDetectionsAsync(PaginationOptionsDTO paginationOptions, IFilterOptions filterOptions)
        {
            // Build URL for root GET: api/detections?{pagination}&{filters}
            var url = $"{api}?{paginationOptions.QueryString}&{filterOptions.QueryString}";
            var httpClient = _httpClientFactory.CreateClient("UnauthenticatedAPI");

            HttpResponseMessage httpResponseMessage;
            try
            {
                httpResponseMessage = await httpClient.GetAsync(url);
            }
            catch (Exception exception) when (exception is HttpRequestException || exception is TaskCanceledException)
            {
                _logger.LogError(exception, "Unable to reach the detections API at {Url}", url);
                return new PaginatedResponseDTO<List<Detection>> { Response = null, TotalAmountPages = 0, TotalNumberRecords = 0, TotalNumberMinutes = 0 };
            }

            if (httpResponseMessage.IsSuccessStatusCode)
            {
                var responseString = await httpResponseMessage.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(responseString))
                {
                    return new PaginatedResponseDTO<List<Detection>> { Response = new List<Detection>(), TotalAmountPages = 0, TotalNumberRecords = 0, TotalNumberMinutes = 0 };
                }

                httpResponseMessage.Headers.TryGetValues("totalAmountPages", out var pageValues);
                httpResponseMessage.Headers.TryGetValues("totalNumberRecords", out var recordValues);
                httpResponseMessage.Headers.TryGetValues("totalNumberMinutes", out var minuteValues);
                int.TryParse(pageValues?.FirstOrDefault(), out var totalAmountPages);
                int.TryParse(recordValues?.FirstOrDefault(), out var totalNumberRecords);
                int.TryParse(minuteValues?.FirstOrDefault(), out var totalNumberMinutes);

                try
                {
                    return new PaginatedResponseDTO<List<Detection>>
                    {
                        Response = JsonSerializer.Deserialize<List<Detection>>(responseString, defaultJsonSerializerOptions),
                        TotalAmountPages = totalAmountPages,
                        TotalNumberRecords = totalNumberRecords,
                        TotalNumberMinutes = totalNumberMinutes
                    };
                }
                catch (JsonException exception)
                {
                    _logger.LogError(exception, "Malformed response from the detections API at {Url}", url);
                    return new PaginatedResponseDTO<List<Detection>> { Response = null, TotalAmountPages = 0, TotalNumberRecords = 0 };
                }
            }
            else
            {
                return new PaginatedResponseDTO<List<Detection>> { Response = null, TotalAmountPages = 0, TotalNumberRecords = 0 };
            }
        }

        // Get unreviewed detections
        public async Task<PaginatedResponseDTO<List<Detection>>> GetCandidateDetectionsAsync(PaginationOptionsDTO paginationOptions, IFilterOptions filterOptions)
        {
            return await GetDetectionsAsync("unreviewed", paginationOptions, filterOptions);
        }

        public async Task<PaginatedResponseDTO<List<Detection>>> GetConfirmedDetectionsAsync(PaginationOptionsDTO paginationOptions, IFilterOptions filterOptions)
        {
            return await GetDetectionsAsync("confirmed", paginationOptions, filterOptions);
        }

        public async Task<PaginatedResponseDTO<List<Detection>>> GetFalseDetectionsAsync(PaginationOptionsDTO paginationOptions, IFilterOptions filterOptions)
        {
            return await GetDetectionsAsync("falsepositives", paginationOptions, filterOptions);
        }

        public async Task<PaginatedResponseDTO<List<Detection>>> GetUnconfirmedDetectionsAsync(PaginationOptionsDTO paginationOptions, IFilterOptions filterOptions)
        {
            return await GetDetectionsAsync("unknowns", paginationOptions, filterOptions);
        }

        public async Task UpdateRequestAsync(DetectionUpdate request)
        {
            var url = $"{api}/{request.Id}";
            var dataJson = JsonSerializer.Serialize(request);
            var stringContent = new StringContent(dataJson, Encoding.UTF8, "application/json");

            var httpClient = _httpClientFactory.CreateClient("AuthenticatedAPI");
            var httpRequest = new HttpRequestMessage(HttpMethod.Put, url) { Content = stringContent };

            _authTokenProvider.ApplyToken(httpRequest);

            HttpResponseMessage httpResponseMessage;
            try
            {
                httpResponseMessage = await httpClient.SendAsync(httpRequest);
            }
            catch (TaskCanceledException exception)
            {
                // A timed-out PUT surfaces as a canceled task. Rethrow it as a
                // request failure so callers handle one exception type for "the
                // update was lost", whether the API refused or timed out.
                _logger.LogError(exception, "Timed out updating the detection at {Url}", url);
                throw new HttpRequestException(
                    $"Failed to update detection. The request to {url} timed out.", exception);
            }
            catch (HttpRequestException exception)
            {
                // The UI suppresses this into a toast, so log it here or the
                // failure never reaches the server diagnostics.
                _logger.LogError(exception, "Unable to reach the detections API to update at {Url}", url);
                throw;
            }

            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                var errorContent = await httpResponseMessage.Content.ReadAsStringAsync();
                var statusCode = (int)httpResponseMessage.StatusCode;

                _logger.LogError("The detections API rejected the update at {Url} with {StatusCode}: {Details}",
                    url, statusCode, errorContent);
                throw new HttpRequestException(
                    $"Failed to update detection. Status: {statusCode} {httpResponseMessage.ReasonPhrase}. Details: {errorContent}");
            }
        }

        public async Task<Detection> GetDetectionAsync(string id)
        {
            var url = $"{api}/{id}";

            // Create client on-demand from the current scope.
            var httpClient = _httpClientFactory.CreateClient("UnauthenticatedAPI");

            HttpResponseMessage httpResponseMessage;
            try
            {
                httpResponseMessage = await httpClient.GetAsync(url);
            }
            catch (Exception exception) when (exception is HttpRequestException || exception is TaskCanceledException)
            {
                // Null means the API could not be reached, so the page can say
                // so instead of misreporting the detection as missing.
                _logger.LogError(exception, "Unable to reach the detections API at {Url}", url);
                return null;
            }

            if (httpResponseMessage.IsSuccessStatusCode)
            {
                var responseString = await httpResponseMessage.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(responseString))
                {
                    return new Detection();
                }

                try
                {
                    var response = JsonSerializer.Deserialize<Detection>(responseString, defaultJsonSerializerOptions);

                    return response ?? new Detection();
                }
                catch (JsonException exception)
                {
                    _logger.LogError(exception, "Malformed response from the detections API at {Url}", url);
                    return null;
                }
            }
            else if (httpResponseMessage.StatusCode == System.Net.HttpStatusCode.NotFound)
            {
                // The API answered 404: this id genuinely has no detection.
                return new Detection();
            }
            else
            {
                // Any other error status is the API failing, not a missing
                // record; report it like an unreachable API.
                _logger.LogError("The detections API returned {StatusCode} at {Url}",
                    (int)httpResponseMessage.StatusCode, url);
                return null;
            }
        }
    }
}
