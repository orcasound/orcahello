namespace OrcaHello.Web.UI.Services
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DetectionService"/> foundation service class for interacting 
    /// with detection-related endpoints of the API.
    /// </summary>
    /// <param name="apiBroker">The detection API broker.</param>
    /// <param name="logger">The logger.</param>
    public partial class DetectionService : IDetectionService
    {
        private readonly IDetectionAPIBroker _apiBroker = null!;
        private readonly ILogger<DetectionService> _logger = null!;

        // Needed for unit testing wrapper to work properly

        public DetectionService() { }

        public DetectionService(
            IDetectionAPIBroker apiBroker,
            ILogger<DetectionService> logger)
        {
            _apiBroker = apiBroker;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves a list of detections, filtered by the state, location, sort by, sort order, date range, page number, 
        /// and page size from the API.
        /// </summary>
        /// <param name="state">The current state of the detection filter.</param>
        /// <param name="fromDate">The start date of the filter.</param>
        /// <param name="toDate">The end date of the filter.</param>
        /// <param name="sortBy">The field the response is to be sorted by.</param>
        /// <param name="isDescending">Flag indicating whether response is to be sorted in descending order.</param>
        /// <param name="page">Which page of the list to return.</param>
        /// <param name="pageSize">The page size to return.</param>
        /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.</returns>
        /// <exception cref="InvalidDetectionException">If one or more of the parameters are invalid.</exception>
        /// <exception cref="NullDetectionResponseException">If the response from the API is null.</exception>
        public ValueTask<DetectionListResponse> RetrieveFilteredAndPaginatedDetectionsAsync(string state, DateTime? fromDate, DateTime? toDate,
            string sortBy, bool isDescending, int page, int pageSize, string location) =>
        TryCatch(async () =>
        {
            Validate(state, nameof(state));
            ValidateDateRange(fromDate, toDate);
            Validate(sortBy, nameof(sortBy));
            ValidatePagination(page, pageSize);

            var queryString = $"&state={state}&fromDate={fromDate.GetValueOrDefault()}&toDate={toDate.GetValueOrDefault()}";
            queryString += $"&sortBy={sortBy}&isDescending={isDescending}";
            queryString += $"&page={page}&pageSize={pageSize}";

            if (!string.IsNullOrWhiteSpace(location))
            {
                queryString += $"&location={location}";
            }

            DetectionListResponse response = await _apiBroker.GetFilteredDetectionsAsync(queryString);

            ValidateResponse(response);

            return response;
        });

        /// <summary>
        /// Retrieves a list of detections, filtered by the tag, date range, page number, 
        /// and page size from the API.
        /// </summary>
        /// <param name="tag">The tag of the detection filter.</param>
        /// <param name="fromDate">The start date of the filter.</param>
        /// <param name="toDate">The end date of the filter.</param>
        /// <param name="page">Which page of the list to return.</param>
        /// <param name="pageSize">The page size to return.</param>
        /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.</returns>
        /// <exception cref="InvalidDetectionException">If one or more of the parameters are invalid.</exception>
        /// <exception cref="NullDetectionResponseException">If the response from the API is null.</exception>
        public ValueTask<DetectionListForTagResponse> RetrieveFilteredAndPaginatedDetectionsForTagAsync(string tag, DateTime? fromDate, DateTime? toDate,
            int page, int pageSize) =>
        TryCatch(async () =>
        {
            Validate(tag, nameof(tag));
            ValidateDateRange(fromDate, toDate);
            ValidatePagination(page, pageSize);

            var queryString = $"fromDate={fromDate.GetValueOrDefault()}&toDate={toDate.GetValueOrDefault()}";
            queryString += $"&page={page}&pageSize={pageSize}";

            DetectionListForTagResponse response = await _apiBroker.GetFilteredDetectionsForTagAsync(tag, queryString);

            ValidateResponse(response);

            return response;
        });

        /// <summary>
        /// Post an update to the API to provide moderation information.
        /// </summary>
        /// <param name="request">The request for update containing appropriate fields.</param>
        /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.</returns>
        /// <exception cref="NullDetectionRequestException">If the passed request is null.</exception>
        /// <exception cref="InvalidDetectionException">If one or more of the parameters are invalid.</exception>
        /// <exception cref="NullDetectionResponseException">If the response from the API is null.</exception>
        public ValueTask<ModerateDetectionsResponse> ModerateDetectionsAsync(ModerateDetectionsRequest request) =>
        TryCatch(async () =>
        {
            ValidateRequest(request);
            Validate(request.State, nameof(request.State));
            Validate(request.Moderator, nameof(request.Moderator));
            ValidateAtLeastOneId(request.Ids, nameof(request.Ids));
            Validate(request.DateModerated, nameof(request.DateModerated));

            ModerateDetectionsResponse response = await _apiBroker.PutModerateDetectionsAsync(request);

            ValidateResponse(response);

            return response;
        });

        /// <summary>
        /// Retrieves a detection for a sepcific Id from the API.
        /// </summary>
        /// <param name="id">The Id of the detection.</param>
        /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.</returns>
        /// <exception cref="InvalidDetectionException">If one or more of the parameters are invalid.</exception>
        /// <exception cref="NullDetectionResponseException">If the response from the API is null.</exception>
        public ValueTask<Detection> RetrieveDetectionAsync(string id) =>
        TryCatch(async () =>
        {
            Validate(id, nameof(id));

            Detection response = await _apiBroker.GetDetectionAsync(id);

            ValidateResponse(response);

            return response;
        });
    }
}