using System;

namespace AIForOrcas.DTO
{
    public class ReviewedFilterOptionsDTO : IFilterOptions
    {
        public string SortOrder { get; set; }
        public string SortBy { get; set; }
        public string Timeframe { get; set; }
        public string Location { get; set; }
        public string HydrophoneId { get; set; }
        public DateTime? DateFrom { get; set; }
        public DateTime? DateTo { get; set; }
        public string QueryString
        {
            get
            {
                var hydrophoneId = string.IsNullOrWhiteSpace(HydrophoneId) ? "all" : HydrophoneId;
                var location = hydrophoneId == "all"
                    ? (string.IsNullOrWhiteSpace(Location) ? "all" : Location)
                    : "all";

                return $"sortBy={SortBy}&sortOrder={SortOrder}&timeframe={Timeframe}&location={location}&hydrophoneId={hydrophoneId}&DateFrom={DateFrom}&DateTo={DateTo}";
            }
        }
    }
}
