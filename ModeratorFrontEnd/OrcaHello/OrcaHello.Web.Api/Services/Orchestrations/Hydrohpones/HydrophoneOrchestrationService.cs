namespace OrcaHello.Web.Api.Services
{
    public partial class HydrophoneOrchestrationService : IHydrophoneOrchestrationService
    {
        private readonly IHydrophoneService _hydrophoneService;
        private readonly ILogger<HydrophoneOrchestrationService> _logger;

        // Needed for unit testing wrapper to work properly

        public HydrophoneOrchestrationService() { }

        public HydrophoneOrchestrationService(IHydrophoneService hydrophoneService,
            ILogger<HydrophoneOrchestrationService> logger)
        {
            _hydrophoneService = hydrophoneService;
            _logger = logger;
        }

        public ValueTask<HydrophoneListResponse> RetrieveHydrophoneLocations() =>
        TryCatch(async () =>
        {
            QueryableHydrophoneData results = await _hydrophoneService.RetrieveAllHydrophonesAsync();

            return new HydrophoneListResponse
            {
                Hydrophones = results.QueryableRecords.Select(h => AsHydrophone(h)).OrderBy(h => h.Name).ToList(),
                Count = results.QueryableRecords.Count()
            };
        });

        [ExcludeFromCodeCoverage]
        private static Hydrophone AsHydrophone(HydrophoneData hydrophoneData)
        {
            var attributes = hydrophoneData?.Attributes;

            if (attributes is not null)
            {
                Hydrophone result = new()
                {
                    Id = attributes.NodeName,
                    Name = attributes.Name,
                    ImageUrl = attributes.ImageUrl,
                    IntroHtml = attributes.IntroHtml
                };

                if (attributes.LocationPoint is not null)
                {
                    if (attributes.LocationPoint.Coordinates != null && attributes.LocationPoint.Coordinates.Count == 2)
                    {
                        result.Longitude = attributes.LocationPoint.Coordinates[0];
                        result.Latitude = attributes.LocationPoint.Coordinates[1];
                    }
                }

                return result;
            }
            else
                return null!;
        }
    }
}
