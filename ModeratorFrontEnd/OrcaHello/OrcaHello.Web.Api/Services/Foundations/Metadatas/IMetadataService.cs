namespace OrcaHello.Web.Api.Services
{
    public interface IMetadataService
    {
        ValueTask<QueryableModerators> RetrieveModeratorsAsync();
        ValueTask<QueryableTagsForTimeframe> RetrieveTagsForGivenTimePeriodAsync(DateTime fromDate, DateTime toDate);
        ValueTask<QueryableTagsForTimeframeAndModerator> RetrieveTagsForGivenTimePeriodAndModeratorAsync(DateTime fromDate, DateTime toDate, string moderator);
        ValueTask<QueryableMetadataForTimeframeAndTag> RetrieveMetadataForGivenTimeframeAndTagAsync(DateTime fromDate, DateTime toDate, string tag, int page, int pageSize);
        ValueTask<QueryableMetadataForTimeframe> RetrievePositiveMetadataForGivenTimeframeAsync(DateTime fromDate, DateTime toDate, int page, int pageSize);
        ValueTask<QueryableMetadataForTimeframeAndModerator> RetrievePositiveMetadataForGivenTimeframeAndModeratorAsync(DateTime fromDate, DateTime toDate,
            string moderator, int page, int pageSize);
        ValueTask<QueryableMetadataForTimeframe> RetrieveNegativeAndUnknownMetadataForGivenTimeframeAsync(DateTime fromDate, DateTime toDate, int page, int pageSize);
        ValueTask<QueryableMetadataForTimeframeAndModerator> RetrieveNegativeAndUnknownMetadataForGivenTimeframeAndModeratorAsync(DateTime fromDate, DateTime toDate,
            string moderator, int page, int pageSize);
        ValueTask<QueryableMetadataForTimeframe> RetrieveUnreviewedMetadataForGivenTimeframeAsync(DateTime fromDate, DateTime toDate, int page, int pageSize);
        ValueTask<MetricsSummaryForTimeframe> RetrieveMetricsForGivenTimeframeAsync(DateTime fromDate, DateTime toDate);
        ValueTask<MetricsSummaryForTimeframeAndModerator> RetrieveMetricsForGivenTimeframeAndModeratorAsync(DateTime fromDate, DateTime toDate,
            string moderator);
        ValueTask<QueryableMetadataForTimeframeTagAndModerator> RetrieveMetadataForGivenTimeframeTagAndModeratorAsync(DateTime fromDate, DateTime toDate,
            string moderator, string tag, int page, int pageSize);
        ValueTask<QueryableMetadataFiltered> RetrievePaginatedMetadataAsync(string state, DateTime fromDate, DateTime toDate, string sortBy,
            bool isDescending, string location, int page, int pageSize);
        ValueTask<Metadata> RetrieveMetadataByIdAsync(string id);
        ValueTask<bool> RemoveMetadataByIdAndStateAsync(string id, string state);
        ValueTask<bool> AddMetadataAsync(Metadata metadata);
        ValueTask<QueryableMetadata> RetrieveMetadataForTagAsync(string tag);
        ValueTask<bool> UpdateMetadataAsync(Metadata metadata);
        ValueTask<QueryableTags> RetrieveAllTagsAsync();
        ValueTask<QueryableMetadata> RetrieveMetadataForInterestLabelAsync(string interestLabel);
        ValueTask<QueryableInterestLabels> RetrieveAllInterestLabelsAsync();
    }
}
