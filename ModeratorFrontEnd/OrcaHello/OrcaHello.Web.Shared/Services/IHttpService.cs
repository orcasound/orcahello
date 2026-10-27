namespace OrcaHello.Web.Shared.Services
{
    public interface IHttpService
    {
        void ClearHeaderValue(string headerName);
        ValueTask DeleteAsync(string url);
        ValueTask<T> DeleteContentAsync<T>(string url);
        ValueTask<T> GetContentAsync<T>(string url);
        ValueTask<T?> PostContentAsync<T>(string url, T content);
        ValueTask<TResult?> PostContentAsync<TContent, TResult>(string url, TContent content);
        Task<T?> PostContentTaskAsync<T>(string url, T content);
        ValueTask PutAsync(string url);
        ValueTask<T> PutContentAsync<T>(string url, T content);
        ValueTask<TResult> PutContentAsync<TContent, TResult>(string url, TContent content);
        void SetHeaderValue(string headerName, string headerValue);
    }
}