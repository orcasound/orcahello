namespace AIForOrcas.DTO.API
{
    /// <summary>
    /// Activity metrics for the specified human moderator.
    /// </summary>
    public class ModeratorMetrics : Metrics
    {
        /// <summary>
        /// Identity of the human moderator (User Principal Name for AzureAD) performing the review.
        /// </summary>
        public string Moderator { get; set; }
    }
}
