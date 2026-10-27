using System;

namespace AIForOrcas.DTO
{
    /// <summary>
    /// Filter and sort options for retrieving candidate (unreviewed) detections.
    /// </summary>
    public class CandidateFilterOptionsDTO : IFilterOptions
    {
        /// <summary>
        /// The sort direction ("asc" or "desc").
        /// </summary>
        public string SortOrder { get; set; }

        /// <summary>
        /// The field to sort results by.
        /// </summary>
        public string SortBy { get; set; }

        /// <summary>
        /// The relative timeframe to filter results by (e.g. "24h"), or "range" to use <see cref="DateFrom"/>/<see cref="DateTo"/>.
        /// </summary>
        public string Timeframe { get; set; }

        /// <summary>
        /// The named location to filter results by, or "all" for no filter.
        /// </summary>
        public string Location { get; set; }

        /// <summary>
        /// The hydrophone to filter results by, or "all" for no filter.
        /// </summary>
        public string HydrophoneId { get; set; }

        /// <summary>
        /// The start of an explicit date range filter, used when <see cref="Timeframe"/> is "range".
        /// </summary>
        public DateTime? DateFrom { get; set; }

        /// <summary>
        /// The end of an explicit date range filter, used when <see cref="Timeframe"/> is "range".
        /// </summary>
        public DateTime? DateTo { get; set; }

        /// <summary>
        /// The filter values formatted as a URL query string.
        /// </summary>
        public string QueryString { get => $"sortBy={SortBy}&sortOrder={SortOrder}&timeframe={Timeframe}&location={Location}&hydrophoneId={HydrophoneId}&dateFrom={DateFrom}&dateTo={DateTo}"; }
    }
}
