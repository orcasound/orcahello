using System;

namespace AIForOrcas.DTO.API
{
    /// <summary>
    /// Query parameters to present to the detections endpoint.
    /// </summary>
    public class DetectionQueryParameters
    {
        /// <summary>
        /// Page number to retrieve.
        /// </summary>
        /// <example>1</example>
        public int Page { get; set; } = 1;

        /// <summary>
        /// Property to sort by (confidence, timestamp).
        /// </summary>
        /// <example>timestamp</example>
        public string SortBy { get; set; } = "timestamp";

        /// <summary>
        /// Order in which to sort the results (asc, desc).
        /// </summary>
        /// <example>desc</example>
        public string SortOrder { get; set; } = "desc";

        /// <summary>
        /// Timeframe for the record set (last 30m, 3h, 6h, 24h, 1w, 1m, range, all).
        /// </summary>
        /// <example>all</example>
        public string Timeframe { get; set; } = "all";

        /// <summary>
        /// Date range filter for from Date (mm/dd/yyyy)
        /// </summary>
        /// <example>12/01/2021</example>
        public DateTime? DateFrom { get; set; }

        /// <summary>
        /// Date range filter for To Date (mm/dd/yyyy)
        /// </summary>
        /// <example>01/15/2022</example>
        public DateTime? DateTo { get; set; }

        /// <summary>
        /// Location of the hydrophone (all, Orcasound Lab, Port Townsend, etc.).
        /// </summary>
        /// <example>all</example>
        public string Location { get; set; } = "all";

        /// <summary>
        /// Hydrophone ID (rpi_orcasound_lab, etc., or all).
        /// </summary>
        /// <example>all</example>
        public string HydrophoneId { get; set; } = "all";

        private static int Clamp(int value, int min, int max) => value < min ? min : value > max ? max : value;

        /// <summary>
        /// Number of records per page to retrieve.
        /// </summary>
        /// <example>5</example>
        public int RecordsPerPage
        {
            get => _recordsPerPage;
            set
            {
                _recordsPerPage = Clamp(value, 0, _maxRecordsPerPage);
            }
        }

        // Zero means "not specified": a request sending only minutesPerPage
        // then pages by minutes, and a request sending neither falls back to
        // the server's default records page size.
        private int _recordsPerPage = 0;
        private readonly int _maxRecordsPerPage = 50;

        /// <summary>
        /// Number of minutes per page to retrieve.
        /// </summary>
        /// <example>0</example>
        public int MinutesPerPage
        {
            get => _minutesPerPage;
            set
            {
                _minutesPerPage = Clamp(value, 0, _maxMinutesPerPage);
            }
        }

        private int _minutesPerPage = 0;
        private readonly int _maxMinutesPerPage = 50;
    }
}
