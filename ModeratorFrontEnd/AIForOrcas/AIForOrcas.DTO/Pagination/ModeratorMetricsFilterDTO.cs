namespace AIForOrcas.DTO.API
{
    /// <summary>
    /// Query parameters for user metrics endpoint.
    /// </summary>
    public class ModeratorMetricsFilterDTO : MetricsFilterDTO
    {
        /// <summary>
        /// Identity of the human moderator (User Principal Name for AzureAD) reviewing metrics.
        /// </summary>
        public string Moderator { get; set; }

        /// <summary>
        /// Constructed queryString.
        /// </summary>
        public override string QueryString => $"moderator={Moderator}&{base.QueryString}";
    }
}
