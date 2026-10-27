namespace AIForOrcas.DTO
{
    /// <summary>
    /// Contract for DTOs that can render themselves as a query string.
    /// </summary>
    public interface IFilterOptions
    {
        /// <summary>
        /// The filter values formatted as a URL query string.
        /// </summary>
        string QueryString { get; }
    }
}
