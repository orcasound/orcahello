namespace AIForOrcas.DTO
{
    /// <summary>
    /// Summary of where a paginated result set currently stands.
    /// </summary>
    public class PaginationResultsDTO
    {
        /// <summary>
        /// The page number currently being viewed.
        /// </summary>
        public int CurrentPage { get; set; } = 1;

        /// <summary>
        /// The total number of pages in the result set.
        /// </summary>
        public int TotalNumberOfPages { get; set; } = 0;

        /// <summary>
        /// The total number of records in the result set.
        /// </summary>
        public int TotalNumberOfRecords { get; set; } = 0;

        /// <summary>
        /// The total number of minutes of audio represented in the result set.
        /// </summary>
        public int TotalNumberOfMinutes { get; set; } = 0;
    }
}
