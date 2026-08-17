namespace OrcaHello.Web.Api.Brokers.Storages
{
    public partial interface IStorageBroker
    {
        Task<List<string>> GetModeratorList();
        Task<List<string>> GetTagListByTimeframe(DateTime fromDate, DateTime toDate);
        Task<List<string>> GetTagListByTimeframeAndModerator(DateTime fromDate, DateTime toDate, string moderator);
        Task<ListMetadataAndCount> GetMetadataListByTimeframeAndTag(DateTime fromDate, DateTime toDate,
            List<string> tags, string tagOperator, int page = 1, int pageSize = 10);
        Task<ListMetadataAndCount> GetPositiveMetadataListByTimeframe(DateTime fromDate, DateTime toDate,
            int page = 1, int pageSize = 10);
        Task<ListMetadataAndCount> GetPositiveMetadataListByTimeframeAndModerator(DateTime fromDate, DateTime toDate,
            string moderator, int page = 1, int pageSize = 10);
        Task<ListMetadataAndCount> GetMetadataListByTimeframeTagAndModerator(DateTime fromDate, DateTime toDate,
                string moderator, string tag, int page = 1, int pageSize = 10);

        Task<ListMetadataAndCount> GetNegativeAndUnknownMetadataListByTimeframe(DateTime fromDate, DateTime toDate,
            int page = 1, int pageSize = 10);
        Task<ListMetadataAndCount> GetNegativeAndUnknownMetadataListByTimeframeAndModerator(DateTime fromDate, DateTime toDate,
            string moderator, int page = 1, int pageSize = 10);
        Task<ListMetadataAndCount> GetUnreviewedMetadataListByTimeframe(DateTime fromDate, DateTime toDate,
            int page = 1, int pageSize = 10);
        Task<List<MetricResult>> GetMetricsListByTimeframe(DateTime fromDate, DateTime toDate);
        Task<List<MetricResult>> GetMetricsListByTimeframeAndModerator(DateTime fromDate, DateTime toDate, string moderator);
        Task<ListMetadataAndCount> GetMetadataListFiltered(string state, DateTime fromDate, DateTime toDate, string sortBy,
            string sortOrder, string location, int page = 1, int pageSize = 1);
        Task<Metadata> GetMetadataById(string id);
        Task<bool> DeleteMetadataByIdAndState(string id, string currentState);
        Task<bool> InsertMetadata(Metadata metadata);
        Task<ListMetadataAndCount> GetAllMetadataListByTag(string tag);
        Task<bool> UpdateMetadataInPartition(Metadata metadata);
        Task<List<string>> GetAllTagList();
        Task<ListMetadataAndCount> GetAllMetadataListByInterestLabel(string interestLabel);
        Task<List<string>> GetAllInterestLabels();
    }
}
