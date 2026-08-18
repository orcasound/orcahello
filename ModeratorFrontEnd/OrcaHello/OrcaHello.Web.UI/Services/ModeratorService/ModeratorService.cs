namespace OrcaHello.Web.UI.Services
{
    /// <summary>
    /// Initializes a new instance of the <see cref="ModeratorService"/> foundation service class for interacting 
    /// with moderator-related endpoints of the API.
    /// </summary>
    /// <param name="apiBroker">The detection API broker.</param>
    /// <param name="logger">The logger.</param>
    public partial class ModeratorService : IModeratorService
    {
        private readonly IDetectionAPIBroker _apiBroker = null!;
        private readonly ILogger<ModeratorService> _logger = null!;


        // Needed for unit testing wrapper to work properly

        public ModeratorService() { }

        public ModeratorService(
        IDetectionAPIBroker apiBroker,
        ILogger<ModeratorService> logger)
        {
            _apiBroker = apiBroker;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves a list of positive comments, filtered by the date range, page number, 
        /// and page size from the API for the given moderator.
        /// </summary>
        /// <param name="moderator">The name of the moderator.</param>
        /// <param name="fromDate">The start date of the filter.</param>
        /// <param name="toDate">The end date of the filter.</param>
        /// <param name="page">Which page of the list to return.</param>
        /// <param name="pageSize">The page size to return.</param>
        /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.</returns>
        /// <exception cref="InvalidModeratorException">If one or more of the parameters are invalid.</exception>
        /// <exception cref="NullModeratorResponseException">If the response from the API is null.</exception>
        public ValueTask<CommentListForModeratorResponse> GetFilteredPositiveCommentsForModeratorAsync(string moderator,
        DateTime? fromDate, DateTime? toDate, int page, int pageSize) =>
        TryCatch(async () =>
        {
            Validate(moderator, nameof(moderator));
            ValidateDateRange(fromDate, toDate);
            ValidatePagination(page, pageSize);

            var queryString = $"fromDate={fromDate.GetValueOrDefault()}&toDate={toDate.GetValueOrDefault()}";
            queryString += $"&page={page}&pageSize={pageSize}";

            CommentListForModeratorResponse response = await _apiBroker
                .GetFilteredPositiveCommentsForModeratorAsync(moderator, queryString);

            ValidateResponse(response);

            return response;
        });

        /// <summary>
        /// Retrieves a list of negative and unknown comments, filtered by the date range, page number, 
        /// and page size from the API for the given moderator.
        /// </summary>
        /// <param name="moderator">The name of the moderator.</param>
        /// <param name="fromDate">The start date of the filter.</param>
        /// <param name="toDate">The end date of the filter.</param>
        /// <param name="page">Which page of the list to return.</param>
        /// <param name="pageSize">The page size to return.</param>
        /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.</returns>
        /// <exception cref="InvalidModeratorException">If one or more of the parameters are invalid.</exception>
        /// <exception cref="NullModeratorResponseException">If the response from the API is null.</exception>
        public ValueTask<CommentListForModeratorResponse> GetFilteredNegativeAndUknownCommentsForModeratorAsync(string moderator,
           DateTime? fromDate, DateTime? toDate, int page, int pageSize) =>
        TryCatch(async () =>
        {
            Validate(moderator, nameof(moderator));
            ValidateDateRange(fromDate, toDate);
            ValidatePagination(page, pageSize);

            var queryString = $"fromDate={fromDate.GetValueOrDefault()}&toDate={toDate.GetValueOrDefault()}";
            queryString += $"&page={page}&pageSize={pageSize}";

            CommentListForModeratorResponse response = await _apiBroker
                .GetFilteredNegativeAndUknownCommentsForModeratorAsync(moderator, queryString);

            ValidateResponse(response);

            return response;
        });

        /// <summary>
        /// Retrieves a list of tags, filtered by the date range from the API for the given moderator.
        /// </summary>
        /// <param name="moderator">The name of the moderator.</param>
        /// <param name="fromDate">The start date of the filter.</param>
        /// <param name="toDate">The end date of the filter.</param>
        /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.</returns>
        /// <exception cref="InvalidModeratorException">If one or more of the parameters are invalid.</exception>
        /// <exception cref="NullModeratorResponseException">If the response from the API is null.</exception>
        public ValueTask<TagListForModeratorResponse> GetFilteredTagsForModeratorAsync(string moderator,
           DateTime? fromDate, DateTime? toDate) =>
        TryCatch(async () =>
        {
            Validate(moderator, nameof(moderator));
            ValidateDateRange(fromDate, toDate);

            var queryString = $"fromDate={fromDate.GetValueOrDefault()}&toDate={toDate.GetValueOrDefault()}";

            TagListForModeratorResponse response = await _apiBroker
               .GetFilteredTagsForModeratorAsync(moderator, queryString);

            ValidateResponse(response);

            return response;
        });

        /// <summary>
        /// Retrieves a list of detections, filtered by the date range, page number, 
        /// and page size from the API for the given moderator.
        /// </summary>
        /// <param name="moderator">The name of the moderator.</param>
        /// <param name="fromDate">The start date of the filter.</param>
        /// <param name="toDate">The end date of the filter.</param>
        /// <param name="page">Which page of the list to return.</param>
        /// <param name="pageSize">The page size to return.</param>
        /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.</returns>
        /// <exception cref="InvalidModeratorException">If one or more of the parameters are invalid.</exception>
        /// <exception cref="NullModeratorResponseException">If the response from the API is null.</exception>
        public ValueTask<DetectionListForModeratorAndTagResponse> GetFilteredDetectionsForTagAndModeratorAsync(string moderator, string tag,
            DateTime? fromDate, DateTime? toDate, int page, int pageSize) =>
        TryCatch(async () =>
        {
            Validate(tag, nameof(tag));
            Validate(moderator, nameof(moderator));
            ValidateDateRange(fromDate, toDate);
            ValidatePagination(page, pageSize);
            var queryString = $"fromDate={fromDate.GetValueOrDefault()}&toDate={toDate.GetValueOrDefault()}";
            queryString += $"&page={page}&pageSize={pageSize}";
            DetectionListForModeratorAndTagResponse response = await _apiBroker
                .GetFilteredDetectionsForTagAndModeratorAsync(moderator, tag, queryString);

            ValidateResponse(response);

            return response;
        });

        /// <summary>
        /// Retrieves the metrics counts (unreviewed, positive, negative, unknown), filtered by the date range from 
        /// the API for the given moderator.
        /// </summary>
        /// <param name="moderator">The name of the moderator.</param>
        /// <param name="fromDate">The start date of the filter.</param>
        /// <param name="toDate">The end date of the filter.</param>
        /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.</returns>
        /// <exception cref="InvalidModeratorException">If one or more of the parameters are invalid.</exception>
        /// <exception cref="NullModeratorResponseException">If the response from the API is null.</exception>
        public ValueTask<MetricsForModeratorResponse> GetFilteredMetricsForModeratorAsync(string moderator,
           DateTime? fromDate, DateTime? toDate) =>
        TryCatch(async () =>
        {
            Validate(moderator, nameof(moderator));
            ValidateDateRange(fromDate, toDate);

            var queryString = $"fromDate={fromDate.GetValueOrDefault()}&toDate={toDate.GetValueOrDefault()}";

            MetricsForModeratorResponse response = await _apiBroker.GetFilteredMetricsForModeratorAsync(moderator, queryString);

            ValidateResponse(response);

            return response;
        });
    }
}
