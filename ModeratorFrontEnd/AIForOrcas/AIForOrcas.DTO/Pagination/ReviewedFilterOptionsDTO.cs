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
        // Dates use an explicit unzoned ISO format rendered invariantly: the
        // default ToString depends on the server locale, and a zone-suffixed
        // format could be shifted by model binding.
        public string QueryString { get => FormattableString.Invariant($"sortBy={SortBy}&sortOrder={SortOrder}&timeframe={Timeframe}&location={Location}&hydrophoneId={HydrophoneId}&DateFrom={DateFrom:yyyy-MM-ddTHH:mm:ss.fffffff}&DateTo={DateTo:yyyy-MM-ddTHH:mm:ss.fffffff}"); }
    }
}
