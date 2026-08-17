namespace OrcaHello.Web.Api.Services
{
    public partial class HydrophoneService : IHydrophoneService
    {
        private readonly IHydrophoneBroker _hydrophoneBroker;
        private readonly ILogger<HydrophoneService> _logger;

        // Needed for unit testing wrapper to work properly
        public HydrophoneService() { }

        public HydrophoneService(IHydrophoneBroker hydrophoneBroker,
            ILogger<HydrophoneService> logger)
        {
            _hydrophoneBroker = hydrophoneBroker;
            _logger = logger;
        }

        public ValueTask<QueryableHydrophoneData> RetrieveAllHydrophonesAsync() =>
        TryCatch(async () =>
        {
            List<HydrophoneData> hydrophoneList = await _hydrophoneBroker.GetFeedsAsync();

            return new QueryableHydrophoneData
            {
                QueryableRecords = hydrophoneList.AsQueryable(),
                TotalCount = hydrophoneList.Count
            };
        });
    }
}
