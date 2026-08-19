namespace OrcaHello.Web.UI.Services
{
    /// <summary>
    /// Partial of the the <see cref="HydrophoneService"/> foundation service class responsible for peforming a generic
    /// TryCatch to marshal level-specific and dependent exceptions.
    /// </summary>
    public partial class HydrophoneService
    {
        public delegate ValueTask<T> ReturningGenericFunction<T>();

        protected async ValueTask<T> TryCatch<T>(ReturningGenericFunction<T> returningGenericFunction)
        {
            try
            {
                // Try to execute the function and return the result.
                return await returningGenericFunction();
            }
            catch (Exception exception)
            {
                // If the exception is related to the validation of the hydrophone response, rethrow
                // it as a HydrophoneValidationException and log it.
                if (exception is NullHydrophoneResponseException ||
                    exception is InvalidHydrophoneException)
                {
                    throw LoggingUtilities.CreateAndLogException<HydrophoneValidationException>(_logger, exception);
                }

                // If the exception is related to the conflict or bad request of the hydrophone broker, rethrow
                // it as a HydrophoneDependencyValidationException and log it.
                if (exception is HttpResponseConflictException)
                {
                    throw LoggingUtilities.CreateAndLogException<HydrophoneDependencyValidationException>(_logger, new AlreadyExistsHydrophoneException(exception));
                }

                if (exception is HttpResponseBadRequestException)
                {
                    throw LoggingUtilities.CreateAndLogException<HydrophoneDependencyValidationException>(_logger, new InvalidHydrophoneException(exception));
                }

                // If the exception is related to the dependency of the hydrophone broker, rethrow
                // it as a HydrophoneDependencyException and log it.
                if (exception is HttpRequestException ||
                    exception is HttpResponseUrlNotFoundException ||
                    exception is HttpResponseUnauthorizedException ||
                    exception is HttpResponseException ||
                    exception is HttpResponseInternalServerErrorException)
                {
                    throw LoggingUtilities.CreateAndLogException<HydrophoneDependencyException>(_logger, new FailedHydrophoneDependencyException(exception));
                }

                // If the exception is any other type, rethrow it as a HydrophoneServiceException and log it.
                throw LoggingUtilities.CreateAndLogException<HydrophoneServiceException>(_logger, new FailedHydrophoneServiceException(exception));
            }
        }
    }
}