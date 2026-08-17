namespace OrcaHello.Web.Api.Services
{
    public partial class TagOrchestrationService
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
                if (exception is InvalidTagOrchestrationException)
                    throw LoggingUtilities.CreateAndLogException<TagOrchestrationValidationException>(_logger, exception);

                if (exception is MetadataValidationException ||
                    exception is MetadataDependencyValidationException)
                    throw LoggingUtilities.CreateAndLogException<TagOrchestrationDependencyValidationException>(_logger, exception);

                if (exception is MetadataDependencyException ||
                    exception is MetadataServiceException)
                    throw LoggingUtilities.CreateAndLogException<TagOrchestrationDependencyException>(_logger, exception);

                throw LoggingUtilities.CreateAndLogException<TagOrchestrationServiceException>(_logger, exception);

            }
        }
    }
}
