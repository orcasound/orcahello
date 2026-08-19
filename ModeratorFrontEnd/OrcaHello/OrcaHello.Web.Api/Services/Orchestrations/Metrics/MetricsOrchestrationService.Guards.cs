namespace OrcaHello.Web.Api.Services
{
    public partial class MetricsOrchestrationService
    {
        protected void Validate(DateTime? date, string propertyName)
        {
            if (!date.HasValue || ValidatorUtilities.IsInvalid(date.Value))
            {
                throw new InvalidMetricOrchestrationException(LoggingUtilities.MissingRequiredProperty(propertyName));
            }
        }
    }
}
