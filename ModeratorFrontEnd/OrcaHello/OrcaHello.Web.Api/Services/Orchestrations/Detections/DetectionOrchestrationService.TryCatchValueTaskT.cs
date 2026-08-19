namespace OrcaHello.Web.Api.Services
{
    public partial class DetectionOrchestrationService
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
                if (exception is NotFoundMetadataException ||
                    exception is DetectionNotDeletedException ||
                    exception is DetectionNotInsertedException ||
                    exception is InvalidDetectionOrchestrationException)
                {
                    throw LoggingUtilities.CreateAndLogException<DetectionOrchestrationValidationException>(_logger, exception);
                }

                if (exception is MetadataValidationException ||
                    exception is MetadataDependencyValidationException)
                {
                    throw LoggingUtilities.CreateAndLogException<DetectionOrchestrationDependencyValidationException>(_logger, exception);
                }

                if (exception is MetadataDependencyException ||
                    exception is MetadataServiceException)
                {
                    throw LoggingUtilities.CreateAndLogException<DetectionOrchestrationDependencyException>(_logger, exception);
                }

                throw LoggingUtilities.CreateAndLogException<DetectionOrchestrationServiceException>(_logger, exception);

            }
        }
    }
}
