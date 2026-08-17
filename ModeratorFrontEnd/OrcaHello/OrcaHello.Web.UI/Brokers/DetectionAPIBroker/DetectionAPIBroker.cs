namespace OrcaHello.Web.UI.Brokers
{
    [ExcludeFromCodeCoverage]
    public partial class DetectionAPIBroker : IDetectionAPIBroker
    {
        private readonly IHttpService _apiClient;
        private readonly AppSettings _appSettings;

        public DetectionAPIBroker(IHttpService apiClient, AppSettings appSettings)
        {
            _apiClient = apiClient;
            _appSettings = appSettings;
        }

        private async ValueTask<T> GetAsync<T>(string relativeUrl) =>
            await _apiClient.GetContentAsync<T>(createFullUrl(relativeUrl));

        private async ValueTask<T> PostAsync<T>(string relativeUrl, T content) =>
            await _apiClient.PostContentAsync<T>(createFullUrl(relativeUrl), content);

        private async ValueTask<TResult> PutAsync<T, TResult>(string relativeUrl, T content) =>
            await _apiClient.PutContentAsync<T, TResult>(createFullUrl(relativeUrl), content);

        private async ValueTask<T> DeleteAsync<T>(string relativeUrl) =>
            await _apiClient.DeleteContentAsync<T>(createFullUrl(relativeUrl));

        private string createFullUrl(string relativeUrl)
        {
            string detectionApiUrn = _appSettings.APIUrl;

            return detectionApiUrn.EndsWith("/") ?
                $"{detectionApiUrn}{relativeUrl}" :
                $"{detectionApiUrn}/{relativeUrl}";
        }
    }
}
