namespace AIForOrcas.DTO
{
    /// <summary>
    /// A single page of results, together with the pagination metadata needed to fetch more.
    /// </summary>
    /// <typeparam name="T">The type of the paginated response payload.</typeparam>
    public class PaginatedResponseDTO<T>
    {
        /// <summary>
        /// The response payload for the current page.
        /// </summary>
        public T Response { get; set; }

        /// <summary>
        /// The total number of pages in the result set.
        /// </summary>
        public int TotalAmountPages { get; set; }

        /// <summary>
        /// The total number of records in the result set.
        /// </summary>
        public int TotalNumberRecords { get; set; }

        /// <summary>
        /// The total number of minutes of audio represented in the result set.
        /// </summary>
        public int TotalNumberMinutes { get; set; }
    }
}
