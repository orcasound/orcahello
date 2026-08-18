namespace OrcaHello.Web.UI.Services
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HydrophoneService"/> foundation service class for interacting 
    /// with hydrophone-related endpoints of the API.
    /// </summary>
    /// <param name="apiBroker">The detection API broker.</param>
    /// <param name="logger">The logger.</param>
    public partial class HydrophoneService : IHydrophoneService
    {
        private readonly IDetectionAPIBroker _apiBroker = null!;
        private readonly ILogger<HydrophoneService> _logger = null!;

        // Needed for unit testing wrapper to work properly

        public HydrophoneService() { }

        public HydrophoneService(
            IDetectionAPIBroker apiBroker,
            ILogger<HydrophoneService> logger)
        {
            _apiBroker = apiBroker;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves information for all hydrophones from the API.
        /// </summary>
        /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.</returns>
        /// <exception cref="InvalidHydrophoneException">If the API returns no hydrophones (empty list).</exception>
        /// <exception cref="NullHydrophoneResponseException">If the response from the API is null.</exception>
        public ValueTask<List<Hydrophone>> RetrieveAllHydrophonesAsync() =>
        TryCatch(async () =>
        {
            HydrophoneListResponse response = await _apiBroker.GetAllHydrophonesAsync();

            ValidateResponse(response);
            ValidateThereAreHydrophones(response.Count);

            return response.Hydrophones;
        });
    }
}
