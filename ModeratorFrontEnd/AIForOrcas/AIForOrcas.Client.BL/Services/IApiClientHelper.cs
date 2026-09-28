using System.Net.Http;
using System.Threading.Tasks;
using AIForOrcas.DTO;
using System.Collections.Generic;

namespace AIForOrcas.Client.BL.Services
{
    public interface IApiClientHelper
    {
        Task<HttpResponseMessage> GetRawAsync(string clientName, string url);
        Task<HttpResponseMessage> SendAuthenticatedAsync(string clientName, HttpRequestMessage request);
        Task<HttpResponseMessage> PutJsonAuthenticatedAsync(string clientName, string url, object payload, IAuthTokenProvider tokenProvider);
        Task<PaginatedResponseDTO<List<T>>> GetPaginatedAsync<T>(string clientName, string url);
        Task<(T Value, HttpResponseMessage Response)> GetJsonAsync<T>(string clientName, string url);
    }
}
