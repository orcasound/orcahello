namespace OrcaHello.Web.Api.Services
{
    public partial class HydrophoneOrchestrationService
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
                if (exception is InvalidHydrophoneOrchestrationException)
                {
                    throw LoggingUtilities.CreateAndLogException<HydrophoneOrchestrationValidationException>(_logger, exception);
                }

                if (exception is HydrophoneValidationException ||
                    exception is HydrophoneDependencyValidationException)
                {
                    throw LoggingUtilities.CreateAndLogException<HydrophoneOrchestrationDependencyValidationException>(_logger, exception);
                }

                if (exception is HydrophoneDependencyException ||
                    exception is HydrophoneServiceException)
                {
                    throw LoggingUtilities.CreateAndLogException<HydrophoneOrchestrationDependencyException>(_logger, exception);
                }

                throw LoggingUtilities.CreateAndLogException<HydrophoneOrchestrationServiceException>(_logger, exception);
            }
        }
    }
}