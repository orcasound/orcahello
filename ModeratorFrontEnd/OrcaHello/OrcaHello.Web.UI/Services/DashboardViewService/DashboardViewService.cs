namespace OrcaHello.Web.UI.Services
{
    /// <summary>
    /// Initializes a new instance of the <see cref="DashboardViewService"/> orchestration service class for rendering 
    /// data from various services for various status and metrics-related views and implementing business logic.
    /// </summary>
    /// <param name="detectionService">The DetectionService foundation service.</param>
    /// <param name="tagService">The TagService foundation service.</param>
    /// <param name="metricsService">The MetricsService foundation service.</param>
    /// <param name="commentService">The CommentService foundation service.</param>
    /// <param name="moderatorService">The ModeratorService foundation service.</param>
    /// <param name="logger">The logger.</param>
    public partial class DashboardViewService : IDashboardViewService
    {
        private readonly IDetectionService _detectionService = null!;
        private readonly ITagService _tagService = null!;
        private readonly IMetricsService _metricsService = null!;
        private readonly ICommentService _commentService = null!;
        private readonly IModeratorService _moderatorService = null!;
        private readonly ILogger<DashboardViewService> _logger = null!;

        // Needed for unit testing wrapper to work properly

        public DashboardViewService() { }

        public DashboardViewService(IDetectionService detectionService,
            ITagService tagService,
            IMetricsService metricsService,
            ICommentService commentService,
            IModeratorService moderatorService,
            ILogger<DashboardViewService> logger)
        {
            _detectionService = detectionService;
            _tagService = tagService;
            _metricsService = metricsService;
            _commentService = commentService;
            _moderatorService = moderatorService;
            _logger = logger;
        }

        #region system metrics

        /// <summary>
        /// Retrieves a list of all tags from TagService, filtered by the date range.
        /// </summary>
        /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.</returns>
        /// <exception cref="NullDashboardViewRequestException">If the request is null.</exception>
        /// <exception cref="InvalidDashboardViewException">If the request is improperly formatted.</exception>
        /// <exception cref="NullDashboardViewResponseException">If the response from TagService is null.</exception>
        public ValueTask<List<string>> RetrieveFilteredTagsAsync(TagsByDateRequest request) =>
        TryCatch(async () =>
        {
            ValidateRequest(request);
            ValidateDateRange(request.FromDate, request.ToDate);

            TagListForTimeframeResponse response =
                await _tagService.RetrieveFilteredTagsAsync(
                    fromDate: request.FromDate,
                    toDate: request.ToDate);

            ValidateResponse(response);

            return response.Tags;
        });

        /// <summary>
        /// Retrieves a list of metrics from MetricsService, filtered by the date range.
        /// </summary>
        /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.</returns>
        /// <exception cref="NullDashboardViewRequestException">If the request is null.</exception>
        /// <exception cref="InvalidDashboardViewException">If the request is improperly formatted.</exception>
        /// <exception cref="NullDashboardViewResponseException">If the response from MetricsService is null.</exception>
        public ValueTask<MetricsItemViewResponse> RetrieveFilteredMetricsAsync(MetricsByDateRequest request) =>
        TryCatch(async () =>
        {
            ValidateRequest(request);
            ValidateDateRange(request.FromDate, request.ToDate);

            MetricsResponse response =
                await _metricsService.RetrieveFilteredMetricsAsync(
            fromDate: request.FromDate,
            toDate: request.ToDate);

            ValidateResponse(response);

            return MetricsItemView.AsMetricsItemViewResponse(response);
        });

        /// <summary>
        /// Retrieves a list of detections from DetectionService for a specific tag, filtered by the date range
        /// and paginated by the page number and size.
        /// </summary>
        /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.</returns>
        /// <exception cref="NullDashboardViewRequestException">If the request is null.</exception>
        /// <exception cref="InvalidDashboardViewException">If the request is improperly formatted.</exception>
        /// <exception cref="NullDashboardViewResponseException">If the response from MetricsService is null.</exception>
        public ValueTask<DetectionItemViewResponse> RetrieveFilteredDetectionsForTagsAsync(PaginatedDetectionsByTagAndDateRequest request) =>
        TryCatch(async () =>
        {
            ValidateRequest(request);
            ValidateTag(request.Tag);
            ValidateDateRange(request.FromDate, request.ToDate);
            ValidatePagination(request.Page, request.PageSize);

            DetectionListForTagResponse response =
            await _detectionService.RetrieveFilteredAndPaginatedDetectionsForTagAsync(
            tag: request.Tag,
            fromDate: request.FromDate,
            toDate: request.ToDate,
            page: request.Page,
            pageSize: request.PageSize);

            ValidateResponse(response);

            return new DetectionItemViewResponse
            {
                DetectionItemViews = response.Detections.Select(DetectionItemView.AsDetectionItemView).ToList(),
                Count = response.TotalCount
            };
        });

        /// <summary>
        /// Retrieves a list of positive comments from CommentService, filtered by the date range
        /// and paginated by the page number and size.
        /// </summary>
        /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.</returns>
        /// <exception cref="NullDashboardViewRequestException">If the request is null.</exception>
        /// <exception cref="InvalidDashboardViewException">If the request is improperly formatted.</exception>
        /// <exception cref="NullDashboardViewResponseException">If the response from CommentService is null.</exception>
        public ValueTask<CommentItemViewResponse> RetrieveFilteredPositiveCommentsAsync(PaginatedCommentsByDateRequest request) =>
        TryCatch(async () =>
        {
            ValidateRequest(request);
            ValidateDateRange(request.FromDate, request.ToDate);
            ValidatePagination(request.Page, request.PageSize);

            CommentListResponse response =
            await _commentService.RetrieveFilteredPositiveCommentsAsync(
            fromDate: request.FromDate,
            toDate: request.ToDate,
            page: request.Page,
            pageSize: request.PageSize);

            ValidateResponse(response);

            return new CommentItemViewResponse
            {
                CommentItemViews = response.Comments.Select(CommentItemView.AsCommentItemView).ToList(),
                Count = response.TotalCount
            };
        });

        /// <summary>
        /// Retrieves a list of negative and unknown comments from CommentService, filtered by the date range
        /// and paginated by the page number and size.
        /// </summary>
        /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.</returns>
        /// <exception cref="NullDashboardViewRequestException">If the request is null.</exception>
        /// <exception cref="InvalidDashboardViewException">If the request is improperly formatted.</exception>
        /// <exception cref="NullDashboardViewResponseException">If the response from CommentService is null.</exception>
        public ValueTask<CommentItemViewResponse> RetrieveFilteredNegativeAndUnknownCommentsAsync(PaginatedCommentsByDateRequest request) =>
        TryCatch(async () =>
        {
            ValidateRequest(request);
            ValidateDateRange(request.FromDate, request.ToDate);
            ValidatePagination(request.Page, request.PageSize);

            CommentListResponse response =
                await _commentService.RetrieveFilteredNegativeAndUnknownCommentsAsync(
                    fromDate: request.FromDate,
                    toDate: request.ToDate,
                    page: request.Page,
                    pageSize: request.PageSize);

            ValidateResponse(response);

            return new CommentItemViewResponse
            {
                CommentItemViews = response.Comments.Select(CommentItemView.AsCommentItemView).ToList(),
                Count = response.TotalCount
            };
        });

        #endregion

        #region moderator metrics

        /// <summary>
        /// Retrieves a list of tags entered for a moderator from ModeratorService, filtered by the date range.
        /// </summary>
        /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.</returns>
        /// <exception cref="NullDashboardViewRequestException">If the request is null.</exception>
        /// <exception cref="InvalidDashboardViewException">If the moderator is invalid or request is improperly formatted.</exception>
        /// <exception cref="NullDashboardViewResponseException">If the response from ModeratorService is null.</exception>
        public ValueTask<List<string>> RetrieveFilteredTagsForModeratorAsync(string moderator, TagsByDateRequest request) =>
        TryCatch(async () =>
        {
            ValidateRequest(request);
            ValidateModerator(moderator);
            ValidateDateRange(request.FromDate, request.ToDate);

            TagListForModeratorResponse response =
                await _moderatorService.GetFilteredTagsForModeratorAsync(
                    moderator: moderator,
                    fromDate: request.FromDate,
                    toDate: request.ToDate);

            ValidateResponse(response);

            return response.Tags;
        });

        /// <summary>
        /// Retrieves a list of metrics for a moderator from ModeratorService, filtered by the date range.
        /// </summary>
        /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.</returns>
        /// <exception cref="NullDashboardViewRequestException">If the request is null.</exception>
        /// <exception cref="InvalidDashboardViewException">If the moderator is invalid or the request is improperly formatted.</exception>
        /// <exception cref="NullDashboardViewResponseException">If the response from ModeratorService is null.</exception>
        public ValueTask<ModeratorMetricsItemViewResponse> RetrieveFilteredMetricsForModeratorAsync(string moderator, MetricsByDateRequest request) =>
         TryCatch(async () =>
         {
             ValidateRequest(request);
             ValidateModerator(moderator);
             ValidateDateRange(request.FromDate, request.ToDate);

             MetricsForModeratorResponse response =
                 await _moderatorService.GetFilteredMetricsForModeratorAsync(
                     moderator: moderator,
                     fromDate: request.FromDate,
                     toDate: request.ToDate);

             ValidateResponse(response);

             return AsModeratorMetricsItemViewResponse(response);
         });

        /// <summary>
        /// Retrieves a list of positive comments for a moderator from ModeratorService, filtered by the date range
        /// and paginated by the page number and size.
        /// </summary>
        /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.</returns>
        /// <exception cref="NullDashboardViewRequestException">If the request is null.</exception>
        /// <exception cref="InvalidDashboardViewException">If the moderator is invalid or the request is improperly formatted.</exception>
        /// <exception cref="NullDashboardViewResponseException">If the response from ModeratorService is null.</exception>
        public ValueTask<ModeratorCommentItemViewResponse> RetrieveFilteredPositiveCommentsForModeratorAsync(string moderator, PaginatedCommentsByDateRequest request) =>
        TryCatch(async () =>
        {
            ValidateRequest(request);
            ValidateModerator(moderator);
            ValidateDateRange(request.FromDate, request.ToDate);
            ValidatePagination(request.Page, request.PageSize);

            CommentListForModeratorResponse response =
                await _moderatorService.GetFilteredPositiveCommentsForModeratorAsync(
                    moderator: moderator,
                    fromDate: request.FromDate,
                    toDate: request.ToDate,
                    page: request.Page,
                    pageSize: request.PageSize);

            ValidateResponse(response);

            return new ModeratorCommentItemViewResponse
            {
                CommentItemViews = response.Comments.Select(CommentItemView.AsCommentItemView).ToList(),
                Count = response.TotalCount,
                Moderator = moderator
            };
        });

        /// <summary>
        /// Retrieves a list of negative and unknown comments for a moderator from ModeratorService, filtered by the date range
        /// and paginated by the page number and size.
        /// </summary>
        /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.</returns>
        /// <exception cref="NullDashboardViewRequestException">If the request is null.</exception>
        /// <exception cref="InvalidDashboardViewException">If the moderator is invalid or the request is improperly formatted.</exception>
        /// <exception cref="NullDashboardViewResponseException">If the response from ModeratorService is null.</exception>
        public ValueTask<ModeratorCommentItemViewResponse> RetrieveFilteredNegativeAndUnknownCommentsForModeratorAsync(string moderator, PaginatedCommentsByDateRequest request) =>
        TryCatch(async () =>
        {
            ValidateRequest(request);
            ValidateModerator(moderator);
            ValidateDateRange(request.FromDate, request.ToDate);
            ValidatePagination(request.Page, request.PageSize);

            CommentListForModeratorResponse response =
                await _moderatorService.GetFilteredNegativeAndUknownCommentsForModeratorAsync(
                    moderator: moderator,
                    fromDate: request.FromDate,
                    toDate: request.ToDate,
                    page: request.Page,
                    pageSize: request.PageSize);

            ValidateResponse(response);

            return new ModeratorCommentItemViewResponse
            {
                CommentItemViews = response.Comments.Select(CommentItemView.AsCommentItemView).ToList(),
                Count = response.TotalCount,
                Moderator = moderator
            };
        });

        /// <summary>
        /// Retrieves a list of detections for a moderator and tag from ModeratorService, filtered by the date range
        /// and paginated by the page number and size.
        /// </summary>
        /// <returns>A <see cref="ValueTask{TResult}"/> that represents the asynchronous operation.</returns>
        /// <exception cref="NullDashboardViewRequestException">If the request is null.</exception>
        /// <exception cref="InvalidDashboardViewException">If the moderator is invalid or the request is improperly formatted.</exception>
        /// <exception cref="NullDashboardViewResponseException">If the response from ModeratorService is null.</exception>
        public ValueTask<ModeratorDetectionItemViewResponse> RetrieveFilteredDetectionsForTagAndModeratorAsync(string moderator, PaginatedDetectionsByTagAndDateRequest request) =>
        TryCatch(async () =>
        {
            ValidateRequest(request);
            ValidateModerator(moderator);
            ValidateTag(request.Tag);
            ValidateDateRange(request.FromDate, request.ToDate);
            ValidatePagination(request.Page, request.PageSize);

            DetectionListForModeratorAndTagResponse response =
                await _moderatorService.GetFilteredDetectionsForTagAndModeratorAsync(
                    moderator: moderator,
                    tag: request.Tag,
                    fromDate: request.FromDate,
                    toDate: request.ToDate,
                    page: request.Page,
                    pageSize: request.PageSize);

            ValidateResponse(response);

            return new ModeratorDetectionItemViewResponse
            {
                DetectionItemViews = response.Detections
                .Select(DetectionItemView.AsDetectionItemView).ToList(),
                Count = response.TotalCount
            };
        });

        // Maps a MetricsForModeratorResponse to a ModeratorMetricsItemViewResponse for display.
        private static Func<MetricsForModeratorResponse, ModeratorMetricsItemViewResponse> AsModeratorMetricsItemViewResponse =>
        metricsResponse => new ModeratorMetricsItemViewResponse
        {
            MetricsItemViews = new List<MetricsItemView>()
            {
                new(DetectionState.Positive.ToString(), metricsResponse.Positive, "#468f57"),
                new(DetectionState.Negative.ToString(), metricsResponse.Negative, "#bb595f"),
                new(DetectionState.Unknown.ToString(), metricsResponse.Unknown, "#bc913e")
            },
            FromDate = metricsResponse.FromDate,
            ToDate = metricsResponse.ToDate,
            Moderator = metricsResponse.Moderator,
        };

        #endregion
    }
}
