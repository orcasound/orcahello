namespace AIForOrcas.DTO
{
    /// <summary>
    /// Options controlling how a result set is paginated.
    /// </summary>
    public class PaginationOptionsDTO
    {
        /// <summary>
        /// The page number to retrieve.
        /// </summary>
        public int Page { get; set; } = 1;

        /// <summary>
        /// The number of records to include per page.
        /// </summary>
        public int RecordsPerPage { get; set; } = 0;

        /// <summary>
        /// The number of minutes of audio to include per page.
        /// </summary>
        public int MinutesPerPage { get; set; } = 10;

        /// <summary>
        /// The number of page-number links to display on either side of the current page.
        /// </summary>
        public int Radius { get; set; } = 3;

        /// <summary>
        /// The pagination options formatted as a URL query string.
        /// </summary>
        public string QueryString { get => $"page={Page}&recordsPerPage={RecordsPerPage}&minutesPerPage={MinutesPerPage}"; }
    }
}
