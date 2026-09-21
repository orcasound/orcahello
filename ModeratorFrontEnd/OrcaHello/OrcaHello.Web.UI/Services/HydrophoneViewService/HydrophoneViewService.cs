using Ganss.Xss;

namespace OrcaHello.Web.UI.Services
{
    /// <summary>
    /// Initializes a new instance of the <see cref="HydrophoneViewService"/> orchestration service class for rendering 
    /// data from HydrophoneService for various hydrophone-related views and implementing hydrophone-related business logic.
    /// </summary>
    /// <param name="hydrophoneService">The HydrophoneService foundation service.</param>
    /// <param name="logger">The logger.</param>
    public partial class HydrophoneViewService : IHydrophoneViewService
    {
        private readonly IHydrophoneService _hydrophoneService = null!;

        private readonly ILogger<HydrophoneViewService> _logger = null!;

        // Needed for unit testing wrapper to work properly

        public HydrophoneViewService() { }

        public HydrophoneViewService(IHydrophoneService hydrophoneService,
            ILogger<HydrophoneViewService> logger)
        {
            _hydrophoneService = hydrophoneService;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves a list of all hydrophones from HydrophoneService and sorts them alphabetically.
        /// </summary>
        /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.</returns>
        /// <exception cref="NullHydrophoneViewResponseException">If the response from HydrophoneService is null.</exception>
        public ValueTask<List<HydrophoneItemView>> RetrieveAllHydrophoneViewsAsync() =>
        TryCatch(async () =>
        {
            List<Hydrophone> response =
                await _hydrophoneService.RetrieveAllHydrophonesAsync();

            ValidateResponse(response);

            return response.Select(AsHydrophoneView).OrderBy(x => x.Name).ToList();
        });

        // Maps a hydrophone to a hydrophone view for presentation.
        private static Func<Hydrophone, HydrophoneItemView> AsHydrophoneView =>
            hydrophone => new HydrophoneItemView
            {
                Id = hydrophone.Id,
                Name = hydrophone.Name,
                Longitude = hydrophone.Longitude,
                Latitude = hydrophone.Latitude,
                ImageUrl = hydrophone.ImageUrl,
                IntroHtml = SanitizeIntroHtml(hydrophone.IntroHtml)
            };

        private static string SanitizeIntroHtml(string introHtml) =>
            string.IsNullOrWhiteSpace(introHtml)
                ? introHtml
                : new HtmlSanitizer().Sanitize(introHtml);
    }
}