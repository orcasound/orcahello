using Microsoft.Extensions.Logging;
using AIForOrcas.DTO;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Net.Http;
using System.Text.Json;
using System.Threading.Tasks;

namespace AIForOrcas.Client.BL.Services
{
    /// <summary>
    /// Shared helper for issuing unauthenticated HTTP GET requests and
    /// parsing paginated/list responses. Centralizes header parsing,
    /// JSON deserialization and network-level error handling so callers
    /// remain consistent.
    /// </summary>
    public class ApiClientHelper : IApiClientHelper
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger _logger;
        private readonly JsonSerializerOptions _jsonOptions = new JsonSerializerOptions() { PropertyNameCaseInsensitive = true };

        /// <summary>
        /// Initializes a new instance of the <see cref="ApiClientHelper"/> class.
        /// </summary>
        /// <param name="httpClientFactory">The HTTP client factory to create HTTP clients.</param>
        /// <param name="logger">The logger instance to log messages.</param>
        /// <exception cref="ArgumentNullException">Thrown when <paramref name="httpClientFactory"/> or <paramref name="logger"/> is null.</exception>
        public ApiClientHelper(IHttpClientFactory httpClientFactory, ILogger logger)
        {
            _httpClientFactory = httpClientFactory ?? throw new ArgumentNullException(nameof(httpClientFactory));
            _logger = logger ?? throw new ArgumentNullException(nameof(logger));
        }

        /// <summary>
        /// GET a raw HTTP response from an unauthenticated endpoint.
        /// </summary>
        /// <param name="clientName">The logical name of the client to create.</param>
        /// <param name="url">The URL of the endpoint to request.</param>
        /// <returns>The raw HTTP response message.</returns>
        public async Task<HttpResponseMessage> GetRawAsync(string clientName, string url)
        {
            var httpClient = _httpClientFactory.CreateClient(clientName);
            try
            {
                return await httpClient.GetAsync(url);
            }
            catch (Exception ex) when (ex is HttpRequestException || ex is TaskCanceledException)
            {
                _logger.LogError(ex, "Unable to reach API at {Url}", url);
                return null;
            }
        }

        /// <summary>
        /// Send an authenticated HTTP request. This method does not swallow
        /// network exceptions so callers (e.g., update operations) can handle
        /// timeouts and retries as appropriate.
        /// </summary>
        /// <param name="clientName">The logical name of the client to create.</param>
        /// <param name="request">The HTTP request message to send.</param>
        /// <returns>The raw HTTP response message.</returns>
        public async Task<HttpResponseMessage> SendAuthenticatedAsync(string clientName, HttpRequestMessage request)
        {
            var httpClient = _httpClientFactory.CreateClient(clientName);
            return await httpClient.SendAsync(request);
        }

        /// <summary>
        /// PUT a JSON payload to an authenticated endpoint. Caller handles exceptions.
        /// </summary>
        /// <param name="clientName">The logical name of the client to create.</param>
        /// <param name="url">The URL of the endpoint to request.</param>
        /// <param name="payload">The object to serialize as JSON and send in the request body.</param>
        /// <param name="tokenProvider">The token provider to apply the authentication token.</param>
        /// <returns>The raw HTTP response message.</returns>
        public async Task<HttpResponseMessage> PutJsonAuthenticatedAsync(string clientName, string url, object payload, IAuthTokenProvider tokenProvider)
        {
            var json = JsonSerializer.Serialize(payload);
            var request = new HttpRequestMessage(HttpMethod.Put, url)
            {
                Content = new StringContent(json, System.Text.Encoding.UTF8, "application/json")
            };

            if (tokenProvider != null)
            {
                tokenProvider.ApplyToken(request);
            }

            return await SendAuthenticatedAsync(clientName, request);
        }

        /// <summary>
        /// GET a paginated response from an unauthenticated endpoint.
        /// </summary>
        /// <typeparam name="T">The type of the items in the paginated response.</typeparam>
        /// <param name="clientName">The logical name of the client to create.</param>
        /// <param name="url">The URL of the endpoint to request.</param>
        /// <returns>The paginated response DTO containing the list of items and pagination metadata.</returns>
        public async Task<PaginatedResponseDTO<List<T>>> GetPaginatedAsync<T>(string clientName, string url)
        {
            var httpResponseMessage = await GetRawAsync(clientName, url);
            if (httpResponseMessage == null)
            {
                return new PaginatedResponseDTO<List<T>> { Response = null, TotalAmountPages = 0, TotalNumberRecords = 0, TotalNumberMinutes = 0 };
            }

            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                return new PaginatedResponseDTO<List<T>> { Response = null, TotalAmountPages = 0, TotalNumberRecords = 0, TotalNumberMinutes = 0 };
            }

            var responseString = await httpResponseMessage.Content.ReadAsStringAsync();

            if (string.IsNullOrWhiteSpace(responseString))
            {
                return new PaginatedResponseDTO<List<T>> { Response = new List<T>(), TotalAmountPages = 0, TotalNumberRecords = 0, TotalNumberMinutes = 0 };
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
                var items = JsonSerializer.Deserialize<List<T>>(responseString, _jsonOptions);
                return new PaginatedResponseDTO<List<T>>
                {
                    Response = items,
                    TotalAmountPages = totalAmountPages,
                    TotalNumberRecords = totalNumberRecords,
                    TotalNumberMinutes = totalNumberMinutes
                };
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Malformed JSON from API at {Url}", url);
                return new PaginatedResponseDTO<List<T>> { Response = null, TotalAmountPages = 0, TotalNumberRecords = 0, TotalNumberMinutes = 0 };
            }
        }

        /// <summary>
        /// GET a JSON response from an unauthenticated endpoint. Returns the deserialized value and the raw HTTP response message.
        /// </summary>
        /// <typeparam name="T">The type of the deserialized value.</typeparam>
        /// <param name="clientName">The logical name of the client to create.</param>
        /// <param name="url">The URL of the endpoint to request.</param>
        /// <returns>A tuple containing the deserialized value and the raw HTTP response message.</returns>
        public async Task<(T Value, HttpResponseMessage Response)> GetJsonAsync<T>(string clientName, string url)
        {
            var httpResponseMessage = await GetRawAsync(clientName, url);
            if (httpResponseMessage == null)
            {
                return (default, null);
            }

            if (!httpResponseMessage.IsSuccessStatusCode)
            {
                return (default, httpResponseMessage);
            }

            var responseString = await httpResponseMessage.Content.ReadAsStringAsync();
            if (string.IsNullOrWhiteSpace(responseString))
            {
                return (default, httpResponseMessage);
            }

            try
            {
                var value = JsonSerializer.Deserialize<T>(responseString, _jsonOptions);
                return (value, httpResponseMessage);
            }
            catch (JsonException ex)
            {
                _logger.LogError(ex, "Malformed JSON from API at {Url}", url);
                return (default, null);
            }
        }
    }
}
