namespace OrcaHello.Web.Api.Services
{
    public partial class ModeratorOrchestrationService : IModeratorOrchestrationService
    {
        private readonly IMetadataService _metadataService;
        private readonly ILogger<ModeratorOrchestrationService> _logger;

        // Needed for unit testing wrapper to work properly

        public ModeratorOrchestrationService() { }

        public ModeratorOrchestrationService(IMetadataService metadataService,
            ILogger<ModeratorOrchestrationService> logger)
        {
            _metadataService = metadataService;
            _logger = logger;
        }

        public ValueTask<ModeratorListResponse> RetrieveModeratorsAsync() =>
        TryCatch(async () =>
        {
            QueryableModerators queryableModerators = await _metadataService.
                RetrieveModeratorsAsync();

            return new ModeratorListResponse
            {
                Moderators = queryableModerators.QueryableRecords.OrderBy(s => s).ToList(),
                Count = queryableModerators.TotalCount
            };
        });

        public ValueTask<MetricsForModeratorResponse> RetrieveMetricsForGivenTimeframeAndModeratorAsync(DateTime? fromDate, DateTime? toDate, string moderator) =>
        TryCatch(async () =>
        {
            Validate(fromDate, nameof(fromDate));
            Validate(toDate, nameof(toDate));
            Validate(moderator, nameof(moderator));

            DateTime nonNullableFromDate = fromDate ?? default;
            DateTime nonNullableToDate = toDate ?? default;

            MetricsSummaryForTimeframeAndModerator results = await _metadataService.
                RetrieveMetricsForGivenTimeframeAndModeratorAsync(nonNullableFromDate, nonNullableToDate, moderator);

            return new MetricsForModeratorResponse
            {
                Positive = results.QueryableRecords.Where(x => x.State == "Positive").Select(x => x.Count).FirstOrDefault(),
                Negative = results.QueryableRecords.Where(x => x.State == "Negative").Select(x => x.Count).FirstOrDefault(),
                Unknown = results.QueryableRecords.Where(x => x.State == "Unknown").Select(x => x.Count).FirstOrDefault(),
                FromDate = results.FromDate,
                ToDate = results.ToDate,
                Moderator = moderator
            };
        });

        public ValueTask<DetectionListForModeratorAndTagResponse> RetrieveDetectionsForGivenTimeframeTagAndModeratorAsync(DateTime? fromDate, DateTime? toDate, string moderator, string tag, int page, int pageSize) =>
        TryCatch(async () =>
        {
            Validate(fromDate, nameof(fromDate));
            Validate(toDate, nameof(toDate));
            Validate(moderator, nameof(moderator));
            Validate(tag, nameof(tag));
            ValidatePage(page);
            ValidatePageSize(pageSize);

            DateTime nonNullableFromDate = fromDate ?? default;
            DateTime nonNullableToDate = toDate ?? default;

            QueryableMetadataForTimeframeTagAndModerator results = await _metadataService.RetrieveMetadataForGivenTimeframeTagAndModeratorAsync
                (nonNullableFromDate, nonNullableToDate, moderator, tag, page, pageSize);

            return new DetectionListForModeratorAndTagResponse
            {
                Detections = results.QueryableRecords.Select(r => DetectionOrchestrationService.AsDetection(r)).ToList(),
                FromDate = results.FromDate,
                ToDate = results.ToDate,
                Moderator = results.Moderator,
                Tag = results.Tag,
                Page = results.Page,
                PageSize = results.PageSize,
                TotalCount = results.TotalCount,
                Count = results.QueryableRecords.Count()
            };
        });

        public ValueTask<TagListForModeratorResponse> RetrieveTagsForGivenTimePeriodAndModeratorAsync(DateTime? fromDate, DateTime? toDate, string moderator) =>
        TryCatch(async () =>
        {
            Validate(fromDate, nameof(fromDate));
            Validate(toDate, nameof(toDate));
            Validate(moderator, nameof(moderator));

            DateTime nonNullableFromDate = fromDate ?? default;
            DateTime nonNullableToDate = toDate ?? default;

            var candidateRecords = await _metadataService.
                RetrieveTagsForGivenTimePeriodAndModeratorAsync(nonNullableFromDate, nonNullableToDate, moderator);

            return new TagListForModeratorResponse
            {
                Tags = candidateRecords.QueryableRecords.OrderBy(s => s).ToList(),
                FromDate = candidateRecords.FromDate,
                ToDate = candidateRecords.ToDate,
                Count = candidateRecords.TotalCount,
                Moderator = moderator
            };
        });

        public ValueTask<CommentListForModeratorResponse> RetrievePositiveCommentsForGivenTimeframeAndModeratorAsync(DateTime? fromDate, DateTime? toDate, string moderator, int page, int pageSize) =>
        TryCatch(async () =>
        {
            Validate(fromDate, nameof(fromDate));
            Validate(toDate, nameof(toDate));
            Validate(moderator, nameof(moderator));
            ValidatePage(page);
            ValidatePageSize(pageSize);

            DateTime nonNullableFromDate = fromDate ?? default;
            DateTime nonNullableToDate = toDate ?? default;

            QueryableMetadataForTimeframeAndModerator results = await _metadataService.
                RetrievePositiveMetadataForGivenTimeframeAndModeratorAsync(nonNullableFromDate, nonNullableToDate, moderator, page, pageSize);

            return new CommentListForModeratorResponse
            {
                Comments = results.QueryableRecords.Select(r => AsComment(r)).ToList(),
                FromDate = results.FromDate,
                ToDate = results.ToDate,
                Page = results.Page,
                PageSize = results.PageSize,
                TotalCount = results.TotalCount,
                Count = results.QueryableRecords.Count(),
                Moderator = moderator,

            };
        });

        public ValueTask<CommentListForModeratorResponse> RetrieveNegativeAndUnknownCommentsForGivenTimeframeAndModeratorAsync(DateTime? fromDate, DateTime? toDate, string moderator, int page, int pageSize) =>
         TryCatch(async () =>
         {
             Validate(fromDate, nameof(fromDate));
             Validate(toDate, nameof(toDate));
             Validate(moderator, nameof(moderator));
             ValidatePage(page);
             ValidatePageSize(pageSize);

             DateTime nonNullableFromDate = fromDate ?? default;
             DateTime nonNullableToDate = toDate ?? default;

             QueryableMetadataForTimeframeAndModerator results = await _metadataService.
                RetrieveNegativeAndUnknownMetadataForGivenTimeframeAndModeratorAsync(nonNullableFromDate, nonNullableToDate, moderator, page, pageSize);

             return new CommentListForModeratorResponse
             {
                 Comments = results.QueryableRecords.Select(r => AsComment(r)).ToList(),
                 FromDate = results.FromDate,
                 ToDate = results.ToDate,
                 Page = results.Page,
                 PageSize = results.PageSize,
                 TotalCount = results.TotalCount,
                 Count = results.QueryableRecords.Count(),
                 Moderator = moderator
             };
         });

        private Comment AsComment(Metadata metadata)
        {
            var comment = new Comment
            {
                Id = metadata.Id,
                Comments = metadata.Comments,
                LocationName = metadata.LocationName,
                Moderator = metadata.Moderator,
                Moderated = !string.IsNullOrWhiteSpace(metadata.DateModerated) ? DateTime.Parse(metadata.DateModerated) : null,
                Timestamp = metadata.Timestamp,
                SpectrogramUri = metadata.ImageUri,
                AudioUri = metadata.AudioUri
            };

            return comment;
        }
    }
}
