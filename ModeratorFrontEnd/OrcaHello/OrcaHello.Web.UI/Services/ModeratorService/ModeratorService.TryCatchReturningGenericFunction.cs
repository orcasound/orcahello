namespace OrcaHello.Web.UI.Services
{
    /// <summary>
    /// Partial of the the <see cref="ModeratorService"/> foundation service class responsible for peforming a generic
    /// TryCatch to marshal level-specific and dependent exceptions.
    /// </summary>
    public partial class ModeratorService
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
                // If the exception is related to the validation of the response, rethrow
                // it as a ModeratorValidationException and log it.
                if (exception is NullModeratorResponseException ||
                    exception is InvalidModeratorException)
                {
                    throw LoggingUtilities.CreateAndLogException<ModeratorValidationException>(_logger, exception);
                }

                // If the exception is related to the conflict or bad request of the broker, rethrow
                // it as a ModeratorDependencyValidationException and log it.
                if (exception is HttpResponseConflictException)
                {
                    throw LoggingUtilities.CreateAndLogException<ModeratorDependencyValidationException>(_logger, new AlreadyExistsModeratorException(exception));
                }

                if (exception is HttpResponseBadRequestException)
                {
                    throw LoggingUtilities.CreateAndLogException<ModeratorDependencyValidationException>(_logger, new InvalidModeratorException(exception));
                }

                // If the exception is related to the dependency of the broker, rethrow
                // it as a ModeratorDependencyException and log it.
                if (exception is HttpRequestException ||
                    exception is HttpResponseUrlNotFoundException ||
                    exception is HttpResponseUnauthorizedException ||
                    exception is HttpResponseException ||
                    exception is HttpResponseInternalServerErrorException)
                {
                    throw LoggingUtilities.CreateAndLogException<ModeratorDependencyException>(_logger, new FailedModeratorDependencyException(exception));
                }

                // If the exception is any other type, rethrow it as a HydrophoneServiceException and log it.
                throw LoggingUtilities.CreateAndLogException<ModeratorServiceException>(_logger, new FailedModeratorServiceException(exception));
            }
        }
    }
}