namespace OrcaHello.Web.UI.Services
{
    /// <summary>
    /// Partial of the the <see cref="MetricsService"/> foundation service class responsible for peforming 
    /// level-specific validations.
    /// </summary>
    public partial class MetricsService
    {
        // RULE: Date range must be valid.
        protected void ValidateDateRange(DateTime? fromDate, DateTime? toDate)
        {
            if (!fromDate.HasValue)
            {
                throw new InvalidMetricsException("Property 'fromDate' cannot be null.");
            }

            if (fromDate.Value > DateTime.UtcNow)
            {
                throw new InvalidMetricsException("Property 'fromDate' cannot be in the future.");
            }

            if (!toDate.HasValue)
            {
                throw new InvalidMetricsException("Property 'toDate' cannot be null.");
            }

            if (toDate.Value < fromDate.Value)
            {
                throw new InvalidMetricsException("Property 'toDate' cannot be before the 'fromDate'.");
            }
        }

        // RULE: Response cannot be null.
        protected void ValidateResponse<T>(T response)
        {
            if (response == null)
            {
                throw new NullMetricsResponseException();
            }
        }
    }
}
