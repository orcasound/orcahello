namespace OrcaHello.Web.UI.Services
{
    /// <summary>
    /// Partial of the the <see cref="MetricsService"/> class responsible for peforming a generic
    /// TryCatch to marshal level-specific and dependent exceptions.
    /// </summary>
    public partial class MetricsService
    {
        public delegate ValueTask<T> ReturningGenericFunction<T>();

        protected async ValueTask<T> TryCatch<T>(ReturningGenericFunction<T> returningGenericFunction)
        {
            try
            {
                return await returningGenericFunction();
            }
            catch (Exception exception)
            {
                // If the exception is related to invalid or null metrics, throw a MetricsValidationException
                if (exception is InvalidMetricsException ||
                    exception is NullMetricsResponseException)
                {
                    throw LoggingUtilities.CreateAndLogException<MetricsValidationException>(_logger, exception);
                }

                // If the exception is related to a conflict in the detection API, throw a MetricsDependencyValidationException with an AlreadyExistsMetricsException
                if (exception is HttpResponseConflictException)
                {
                    throw LoggingUtilities.CreateAndLogException<MetricsDependencyValidationException>(_logger, new AlreadyExistsMetricsException(exception));
                }

                // If the exception is related to a bad request in the detection API, throw a MetricsDependencyValidationException with an InvalidMetricsException
                if (exception is HttpResponseBadRequestException)
                {
                    throw LoggingUtilities.CreateAndLogException<MetricsDependencyValidationException>(_logger, new InvalidMetricsException(exception));
                }

                // If the exception is related to any other HTTP error in the detection API, throw a MetricsDependencyException with a FailedMetricsDependencyException
                if (exception is HttpRequestException ||
                    exception is HttpResponseUrlNotFoundException ||
                    exception is HttpResponseUnauthorizedException ||
                    exception is HttpResponseInternalServerErrorException ||
                    exception is HttpResponseException)
                {
                    throw LoggingUtilities.CreateAndLogException<MetricsDependencyException>(_logger, new FailedMetricsDependencyException(exception));
                }

                // If the exception is not handled by any of the above cases, throw a MetricsServiceException with a FailedMetricsServiceException
                throw LoggingUtilities.CreateAndLogException<MetricsServiceException>(_logger, new FailedMetricsServiceException(exception));
            }

        }
    }
}
