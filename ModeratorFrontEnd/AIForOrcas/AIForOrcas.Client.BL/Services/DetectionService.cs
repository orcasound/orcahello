using AIForOrcas.DTO;
using AIForOrcas.DTO.API;
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

        public DetectionService(IHttpClientFactory httpClientFactory, IAuthTokenProvider authTokenProvider, ILogger<DetectionService> logger)
        {
            _httpClientFactory = httpClientFactory;
            _authTokenProvider = authTokenProvider;
            _logger = logger;
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
                return new PaginatedResponseDTO<List<Detection>> { Response = null, TotalAmountPages = 0, TotalNumberRecords = 0 };
            }

            if (httpResponseMessage.IsSuccessStatusCode)
            {
                var responseString = await httpResponseMessage.Content.ReadAsStringAsync();

                if (string.IsNullOrWhiteSpace(responseString))
                {
                    return new PaginatedResponseDTO<List<Detection>> { Response = new List<Detection>(), TotalAmountPages = 0, TotalNumberRecords = 0 };
                }

                // The pagination headers are not guaranteed; a response without
                // them should not kill the page.
                httpResponseMessage.Headers.TryGetValues("totalAmountPages", out var pageValues);
                httpResponseMessage.Headers.TryGetValues("totalNumberRecords", out var recordValues);
                int.TryParse(pageValues?.FirstOrDefault(), out var totalAmountPages);
                int.TryParse(recordValues?.FirstOrDefault(), out var totalNumberRecords);

                try
                {
                    return new PaginatedResponseDTO<List<Detection>>
                    {
                        Response = JsonSerializer.Deserialize<List<Detection>>(responseString, defaultJsonSerializerOptions),
                        TotalAmountPages = totalAmountPages,
                        TotalNumberRecords = totalNumberRecords
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
