using System;

namespace AIForOrcas.DTO
{
    public class CandidateFilterOptionsDTO : IFilterOptions
    {
        public string SortOrder { get; set; }
        public string SortBy { get; set; }
        public string Timeframe { get; set; }
        public string Location { get; set; }
        public string HydrophoneId { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string QueryString { get => $"sortBy={SortBy}&sortOrder={SortOrder}&timeframe={Timeframe}&location={Location}&hydrophoneId={HydrophoneId}&dateFrom={DateFrom}&dateTo={DateTo}"; }
    }
}
