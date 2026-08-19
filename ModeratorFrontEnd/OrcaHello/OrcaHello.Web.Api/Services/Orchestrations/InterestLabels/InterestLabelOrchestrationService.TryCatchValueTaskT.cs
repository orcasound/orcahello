namespace OrcaHello.Web.Api.Services
{
    public partial class InterestLabelOrchestrationService
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
                    exception is InvalidInterestLabelOrchestrationException)
                {
                    throw LoggingUtilities.CreateAndLogException<InterestLabelOrchestrationValidationException>(_logger, exception);
                }

                if (exception is MetadataValidationException ||
                    exception is MetadataDependencyValidationException)
                {
                    throw LoggingUtilities.CreateAndLogException<InterestLabelOrchestrationDependencyValidationException>(_logger, exception);
                }

                if (exception is MetadataDependencyException ||
                    exception is MetadataServiceException)
                {
                    throw LoggingUtilities.CreateAndLogException<InterestLabelOrchestrationDependencyException>(_logger, exception);
                }

                throw LoggingUtilities.CreateAndLogException<InterestLabelOrchestrationServiceException>(_logger, exception);

            }
        }
    }
}
