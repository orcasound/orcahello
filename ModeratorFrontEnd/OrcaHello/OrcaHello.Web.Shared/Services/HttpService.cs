using System.Text.Json.Serialization;

namespace OrcaHello.Web.Shared.Services
{
    [ExcludeFromCodeCoverage]
    public class HttpService : IHttpService
    {
        private readonly HttpClient _httpClient;
        private JsonSerializerOptions _jsonSerializeOptions = new()
        {
            PropertyNameCaseInsensitive = true,
            DefaultIgnoreCondition = JsonIgnoreCondition.WhenWritingNull,
        };

        public HttpService(HttpClient httpClient)
        {
            _httpClient = httpClient;
        }

        public void SetHeaderValue(string headerName, string headerValue)
        {
            ClearHeaderValue(headerName);
            _httpClient.DefaultRequestHeaders.Add(headerName, headerValue);
        }

        public void ClearHeaderValue(string headerName)
        {
            _httpClient.DefaultRequestHeaders.Remove(headerName);
        }

        public ValueTask<T> PostContentAsync<T>(string url, T content) =>
            PostContentAsync<T, T>(url, content);

        public Task<T> PostContentTaskAsync<T>(string url, T content) =>
            PostContentAsync<T, T>(url, content).AsTask();

        private Uri NormalizeUrl(string url)
        {
            if (url.StartsWith("HTTP", StringComparison.OrdinalIgnoreCase))
            {
                return new Uri(url);
            }
            else
            {
                return new Uri(url, UriKind.Relative);
            }
        }

        public async ValueTask<TResult> PostContentAsync<TContent, TResult>(string url, TContent content)
        {
            var dataJson = System.Text.Json.JsonSerializer.Serialize(content, _jsonSerializeOptions);
            var stringContent = new StringContent(dataJson, Encoding.UTF8, "application/json");
            HttpResponseMessage responseMessage =
                await _httpClient.PostAsync(NormalizeUrl(url), stringContent);

            var message = await responseMessage.Content.ReadAsStringAsync();

            await ValidationService.ValidateHttpResponseAsync(responseMessage);

            if (ValidationService.OKWithNoContent(responseMessage))
            {
                return default(TResult);
            }

            return await Deserialize<TResult>(responseMessage);
        }

        public async ValueTask<T> PutContentAsync<T>(string url, T content)
        {
            var dataJson = System.Text.Json.JsonSerializer.Serialize(content, _jsonSerializeOptions);
            var stringContent = new StringContent(dataJson, Encoding.UTF8, "application/json");
            HttpResponseMessage responseMessage =
                await _httpClient.PutAsync(NormalizeUrl(url), stringContent);

            await ValidationService.ValidateHttpResponseAsync(responseMessage);

            return await Deserialize<T>(responseMessage);
        }

        public async ValueTask<TResult> PutContentAsync<TContent, TResult>(string url, TContent content)
        {
            var dataJson = System.Text.Json.JsonSerializer.Serialize(content, _jsonSerializeOptions);
            var stringContent = new StringContent(dataJson, Encoding.UTF8, "application/json");
            HttpResponseMessage responseMessage =
                await _httpClient.PutAsync(NormalizeUrl(url), stringContent);

            await ValidationService.ValidateHttpResponseAsync(responseMessage);

            return await Deserialize<TResult>(responseMessage);
        }

        public async ValueTask PutAsync(string url)
        {
            HttpResponseMessage responseMessage =
                await _httpClient.PutAsync(NormalizeUrl(url), null);

            await ValidationService.ValidateHttpResponseAsync(responseMessage);
        }

        public async ValueTask<T> GetContentAsync<T>(string url)
        {
            HttpResponseMessage responseMessage =
                await _httpClient.GetAsync(NormalizeUrl(url));

            await ValidationService.ValidateHttpResponseAsync(responseMessage);

            return await Deserialize<T>(responseMessage);
        }

        public async ValueTask<T> DeleteContentAsync<T>(string url)
        {
            HttpResponseMessage responseMessage =
                await _httpClient.DeleteAsync(NormalizeUrl(url));

            await ValidationService.ValidateHttpResponseAsync(responseMessage);

            return await Deserialize<T>(responseMessage);
        }

        public async ValueTask DeleteAsync(string url)
        {
            HttpResponseMessage responseMessage =
                await _httpClient.DeleteAsync(NormalizeUrl(url));

            await ValidationService.ValidateHttpResponseAsync(responseMessage);
        }

        private async Task<T> Deserialize<T>(HttpResponseMessage httpResponseMessage)
        {
            var responseString = await httpResponseMessage.Content.ReadAsStringAsync();
            var returnObject = System.Text.Json.JsonSerializer.Deserialize<T>(responseString, _jsonSerializeOptions);
            return returnObject;
        }

    }
}
