namespace OrcaHello.Web.Api.Services
{
    public partial class ModeratorOrchestrationService
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
                if (exception is InvalidModeratorOrchestrationException)
                {
                    throw LoggingUtilities.CreateAndLogException<ModeratorOrchestrationValidationException>(_logger, exception);
                }

                if (exception is MetadataValidationException ||
                    exception is MetadataDependencyValidationException)
                {
                    throw LoggingUtilities.CreateAndLogException<ModeratorOrchestrationDependencyValidationException>(_logger, exception);
                }

                if (exception is MetadataDependencyException ||
                    exception is MetadataServiceException)
                {
                    throw LoggingUtilities.CreateAndLogException<ModeratorOrchestrationDependencyException>(_logger, exception);
                }

                throw LoggingUtilities.CreateAndLogException<ModeratorOrchestrationServiceException>(_logger, exception);

            }
        }
    }
}
