namespace OrcaHello.Web.UI.Services
{
    /// <summary>
    /// Partial of the the <see cref="HydrophoneViewService"/> orchestration service class responsible for peforming a generic
    /// TryCatch to marshal level-specific and dependent exceptions.
    /// </summary>
    public partial class HydrophoneViewService
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
                // If the exception is related to the validation of the hydrophone view, rethrow
                // it as a HydrophoneViewValidationException and log it.
                if (exception is NullHydrophoneViewResponseException ||
                    exception is InvalidHydrophoneViewException)
                {
                    throw LoggingUtilities.CreateAndLogException<HydrophoneViewValidationException>(_logger, exception);
                }

                // If the exception is related to the validation of the hydrophone dependency, rethrow
                // it as a HydrophoneViewDependencyValidationException and log it.
                if (exception is HydrophoneValidationException ||
                    exception is HydrophoneDependencyValidationException)
                {
                    throw LoggingUtilities.CreateAndLogException<HydrophoneViewDependencyValidationException>(_logger, exception);
                }

                // If the exception is related to the dependency of the hydrophone service, rethrow
                // it as a HydrophoneViewDependencyException and log it.
                if (exception is HydrophoneDependencyException ||
                    exception is HydrophoneServiceException)
                {
                    throw LoggingUtilities.CreateAndLogException<HydrophoneViewDependencyException>(_logger, exception);
                }

                // If the exception is any other type, rethrow it as a HydrophoneViewServiceException and log it.
                throw LoggingUtilities.CreateAndLogException<HydrophoneViewServiceException>(_logger, exception);
            }
        }
    }
}