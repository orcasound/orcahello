namespace OrcaHello.Web.Api.Services
{
    public partial class TagOrchestrationService : ITagOrchestrationService
    {
        private readonly IMetadataService _metadataService;
        private readonly ILogger<TagOrchestrationService> _logger;

        // Needed for unit testing wrapper to work properly

        public TagOrchestrationService() { }

        public TagOrchestrationService(IMetadataService metadataService,
            ILogger<TagOrchestrationService> logger)
        {
            _metadataService = metadataService;
            _logger = logger;
        }

        public ValueTask<TagListForTimeframeResponse> RetrieveTagsForGivenTimePeriodAsync(DateTime? fromDate, DateTime? toDate) =>
        TryCatch(async () =>
        {
            Validate(fromDate, nameof(fromDate));
            Validate(toDate, nameof(toDate));

            DateTime nonNullableFromDate = fromDate ?? default;
            DateTime nonNullableToDate = toDate ?? default;

            var candidateRecords = await _metadataService.
                RetrieveTagsForGivenTimePeriodAsync(nonNullableFromDate, nonNullableToDate);

            return new TagListForTimeframeResponse
            {
                Tags = candidateRecords.QueryableRecords.OrderBy(s => s).ToList(),
                FromDate = candidateRecords.FromDate,
                ToDate = candidateRecords.ToDate,
                Count = candidateRecords.TotalCount
            };
        });

        public ValueTask<TagListResponse> RetrieveAllTagsAsync() =>
        TryCatch(async () =>
        {
            var candidateRecords = await _metadataService.RetrieveAllTagsAsync();

            return new TagListResponse
            {
                Tags = candidateRecords.QueryableRecords.OrderBy(s => s).ToList(),
                Count = candidateRecords.TotalCount
            };
        });

        public ValueTask<TagRemovalResponse> RemoveTagFromAllDetectionsAsync(string tagToRemove) =>
        TryCatch(async () =>
        {
            Validate(tagToRemove, nameof(tagToRemove));

            var allMetadataWithTag = await _metadataService.RetrieveMetadataForTagAsync(tagToRemove);

            int totalRemoved = 0;

            foreach (Metadata item in allMetadataWithTag.QueryableRecords)
            {
                item.Tags.Remove(tagToRemove);

                bool recordUpdated = await _metadataService.UpdateMetadataAsync(item);

                if (recordUpdated)
                    totalRemoved++;
            }

            TagRemovalResponse result = new()
            {
                Tag = tagToRemove,
                TotalMatching = allMetadataWithTag.TotalCount,
                TotalRemoved = totalRemoved
            };

            return result;
        });

        public ValueTask<TagReplaceResponse> ReplaceTagInAllDetectionsAsync(ReplaceTagRequest request) =>
        TryCatch(async () =>
        {
            Validate(request.OldTag, nameof(request.OldTag));
            Validate(request.NewTag, nameof(request.NewTag));

            var allMetadataWithTag = await _metadataService.RetrieveMetadataForTagAsync(request.OldTag);

            int totalReplaced = 0;

            foreach (Metadata item in allMetadataWithTag.QueryableRecords)
            {
                int indexOfOldTag = item.Tags.IndexOf(request.OldTag);
                if (indexOfOldTag >= 0)
                {
                    item.Tags[indexOfOldTag] = request.NewTag;
                }

                bool recordUpdated = await _metadataService.UpdateMetadataAsync(item);

                if (recordUpdated)
                    totalReplaced++;
            }

            TagReplaceResponse result = new()
            {
                OldTag = request.OldTag,
                NewTag = request.NewTag,
                TotalMatching = allMetadataWithTag.TotalCount,
                TotalReplaced = totalReplaced
            };

            return result;
        });
    }
}
