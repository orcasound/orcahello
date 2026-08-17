namespace OrcaHello.Web.UI.Services
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DetectionViewService"/> orchestration service class for rendering 
    /// data from TagService and DetectionService for various detection-related views and implementing detection-related business logic.
    /// </summary>
    /// <param name="tagService">The TagService foundation service.</param>
    /// <param name="detectionService"> The DetectionService foundation service.</param>
    /// <param name="logger">The logger.</param>
    public partial class DetectionViewService : IDetectionViewService
    {
        private readonly IDetectionService _detectionService = null!;
        private readonly ITagService _tagService = null!;
        private readonly ILogger<DetectionViewService> _logger = null!;

        // Needed for unit testing wrapper to work properly

        public DetectionViewService() { }

        public DetectionViewService(IDetectionService detectionService,
            ITagService tagService,
            ILogger<DetectionViewService> logger)
        {
            _detectionService = detectionService;
            _tagService = tagService;
            _logger = logger;
        }

        /// <summary>
        /// Retrieves a list of detections from DetectionService, filtered by state, date range, sort field, and pagination.
        /// </summary>
        /// <param name="options">The filtering options to define the search.</param>
        /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.</returns>
        /// <exception cref="NullDetectionViewRequestException">If request is null.</exception>
        /// <exception cref="InvalidDetectionViewException">If one of the request properties is null or invalid.</exception>
        /// <exception cref="NullDetectionViewResponseException">If the response from DetectionService is null.</exception>
        public ValueTask<DetectionItemViewResponse> RetrieveFilteredAndPaginatedDetectionItemViewsAsync(PaginatedDetectionsByStateRequest request) =>
        TryCatch(async () =>
        {
            ValidateRequest(request);
            Validate(request.State, nameof(request.State));
            Validate(request.SortBy, nameof(request.SortBy));
            ValidateDateRange(request.FromDate, request.ToDate);
            ValidatePagination(request.Page, request.PageSize);

            DetectionListResponse response =
                await _detectionService.RetrieveFilteredAndPaginatedDetectionsAsync(
                    state: request.State,
                    fromDate: request.FromDate,
                    toDate: request.ToDate,
                    sortBy: request.SortBy,
                    isDescending: request.IsDescending,
                    location: request.Location,
                    page: request.Page,
                    pageSize: request.PageSize);

            ValidateResponse(response);

            return new DetectionItemViewResponse
            {
                DetectionItemViews = response.Detections.Select(AsDetectionItemView).ToList(),
                Count = response.TotalCount
            };
        });

        /// <summary>
        /// Post a detection moderation to DetectionService.
        /// </summary>
        /// <param name="ids">The ids or one or more detections to update (bulk or single).</param>
        /// <param name="state">The detection state to set.</param>
        /// <param name="moderator">The name of the moderator to set.</param>
        /// <param name="comments">The comments to set.</param>
        /// <param name="tags">The list of tags to set.</param>
        /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.</returns>
        /// <exception cref="InvalidDetectionViewException">If one of the passed parameters is null or invalid.</exception>
        /// <exception cref="NullDetectionViewResponseException">If the response from DetectionService is null.</exception>
        public ValueTask<ModerateDetectionsResponse> ModerateDetectionsAsync(List<string> ids, string state, string moderator,
            string comments, string tags) =>
        TryCatch(async () =>
        {
            ValidateAtLeastOneId(ids);
            Validate(state, nameof(state));
            Validate(moderator, nameof(moderator));

            var commentsString = !string.IsNullOrWhiteSpace(comments) ? comments : string.Empty;
            var tagsList = !string.IsNullOrWhiteSpace(tags) ?
                tags.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries).ToList() :
                new List<string>();

            ModerateDetectionsRequest request = new()
            {
                Ids = ids,
                State = state,
                Moderator = moderator,
                DateModerated = DateTime.UtcNow,
                Comments = commentsString,
                Tags = tagsList
            };

            ModerateDetectionsResponse response =
                await _detectionService.ModerateDetectionsAsync(request);

            ValidateResponse(response);

            return response;
        });

        /// <summary>
        /// Retrieves a list of all tags from TagService and sorts them alphabetically.
        /// </summary>
        /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.</returns>
        /// <exception cref="NullTagViewResponseException">If the response from TagService is null.</exception>
        public ValueTask<List<string>> RetrieveAllTagsAsync() =>
        TryCatch(async () =>
        {
            var response = await _tagService.RetrieveAllTagsAsync();

            ValidateResponse(response);

            return response.OrderBy(x => x).ToList();
        });

        /// <summary>
        /// Retrieves a single detection by id.
        /// </summary>
        /// <param name="id">The id associated with the requested detection.</param>
        /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.</returns>
        /// <exception cref="NullTagViewResponseException">If the response from TagService is null.</exception>
        public ValueTask<DetectionItemView> RetrieveDetectionAsync(string id) =>
        TryCatch(async () =>
        {
            Validate(id, nameof(id));

            Detection response = await _detectionService.RetrieveDetectionAsync(id);

            ValidateResponse(response);

            return AsDetectionItemView(response);
        });

        // Maps a Detection to a DetectionItemView for presentation.
        private static Func<Detection, DetectionItemView> AsDetectionItemView =>
            detection => new DetectionItemView
            {
                Id = detection.Id,
                LocationName = detection.LocationName,
                Timestamp = detection.Timestamp,
                AudioUri = detection.AudioUri,
                SpectrogramUri = detection.SpectrogramUri,
                Confidence = detection.Confidence,
                State = detection.State,
                Location = new()
                {
                    Name = detection.Location.Name,
                    Longitude = detection.Location.Longitude,
                    Latitude = detection.Location.Latitude
                },
                Comments = detection.Comments,
                Moderator = detection.Moderator,
                Moderated = detection.Moderated,
                Tags = String.Join(", ", detection.Tags),
                Annotations = detection.Annotations.Select(AsAnnotationItemView).ToList(),
                InterestLabel = detection.InterestLabel
            };

        // Maps an Annotation to an AnnotationItemView for presentation.
        private static Func<Annotation, AnnotationItemView> AsAnnotationItemView =>
            annotation => new AnnotationItemView
            {
                Id = annotation.Id,
                StartTime = annotation.StartTime,
                EndTime = annotation.EndTime,
                Confidence = annotation.Confidence
            };
    }
}