namespace OrcaHello.Web.Api.Brokers.Storages
{
    [ExcludeFromCodeCoverage]
    public partial class StorageBroker : IStorageBroker, IDisposable
    {
        private readonly AppSettings _appSettings;
        private readonly CosmosClient _cosmosClient;
        private readonly Container _detectionsContainer;

        public StorageBroker(AppSettings appSettings)
        {
            _appSettings = appSettings;

            _cosmosClient = new CosmosClient(_appSettings.CosmosConnectionString);

            Database database;

            try
            {
                database = _cosmosClient.GetDatabase(_appSettings.DetectionsDatabaseName);
                database.ReadAsync().Wait();
            }
            catch (Exception exception)
            {
                throw new Exception($"Database '{_appSettings.DetectionsDatabaseName}' was not found or could not be opened: {exception.Message}");
            }

            try
            {
                _detectionsContainer = database.GetContainer(_appSettings.MetadataContainerName);
                _detectionsContainer.ReadContainerAsync().Wait();
            }
            catch (Exception exception)
            {
                throw new Exception($"Container '{_appSettings.MetadataContainerName}' was not found or could not be opened: {exception.Message}.");
            }
        }

        public void Dispose()
        {
            _cosmosClient.Dispose();
            GC.SuppressFinalize(this);
        }
    }
}
