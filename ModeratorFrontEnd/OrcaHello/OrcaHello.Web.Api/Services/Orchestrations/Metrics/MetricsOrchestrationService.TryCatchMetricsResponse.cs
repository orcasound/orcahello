namespace OrcaHello.Web.Api.Services
{
    public partial class MetricsOrchestrationService
    {
        public delegate ValueTask<MetricsResponse> ReturningMetricsResponseFunction();

        protected async ValueTask<MetricsResponse> TryCatch(ReturningMetricsResponseFunction returningMetricsResponseFunction)
        {
            try
            {
                return await returningMetricsResponseFunction();
            }
            catch (Exception exception)
            {
                if (exception is InvalidMetricOrchestrationException)
                {
                    throw LoggingUtilities.CreateAndLogException<MetricOrchestrationValidationException>(_logger, exception);
                }

                if (exception is MetadataValidationException ||
                    exception is MetadataDependencyValidationException)
                {
                    throw LoggingUtilities.CreateAndLogException<MetricOrchestrationDependencyValidationException>(_logger, exception);
                }

                if (exception is MetadataDependencyException ||
                    exception is MetadataServiceException)
                {
                    throw LoggingUtilities.CreateAndLogException<MetricOrchestrationDependencyException>(_logger, exception);
                }

                throw LoggingUtilities.CreateAndLogException<MetricOrchestrationServiceException>(_logger, exception);

            }
        }
    }
}
