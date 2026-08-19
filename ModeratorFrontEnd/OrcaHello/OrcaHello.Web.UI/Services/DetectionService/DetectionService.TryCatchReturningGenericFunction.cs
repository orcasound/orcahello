namespace OrcaHello.Web.UI.Services
{
    /// <summary>
    /// Partial of the the <see cref="DetectionService"/> foundation service class responsible for peforming a generic
    /// TryCatch to marshal level-specific and dependent exceptions.
    /// </summary>
    public partial class DetectionService
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
                // If the exception is any of the following types, rethrow it as a TagValidationException
                if (exception is InvalidDetectionException ||
                    exception is NullDetectionRequestException ||
                    exception is NullDetectionResponseException)
                {
                    throw LoggingUtilities.CreateAndLogException<DetectionValidationException>(_logger, exception);
                }

                // If the exception is an HttpResponseConflictException, rethrow it as a DetectionDependencyValidationException with an AlreadyExistsTagException as the inner exception
                if (exception is HttpResponseConflictException)
                {
                    throw LoggingUtilities.CreateAndLogException<DetectionDependencyValidationException>(_logger, new AlreadyExistsDetectionException(exception));
                }

                // If the exception is an HttpResponseBadRequestException, rethrow it as a DetectionDependencyValidationException with an InvalidDetectionException as the inner exception
                if (exception is HttpResponseBadRequestException)
                {
                    throw LoggingUtilities.CreateAndLogException<DetectionDependencyValidationException>(_logger, new InvalidDetectionException(exception));
                }

                // If the exception is any of the following types, rethrow it as a DetectionDependencyException with a FailedDetectionDependencyException as the inner exception
                if (exception is HttpRequestException ||
                    exception is HttpResponseUrlNotFoundException ||
                    exception is HttpResponseUnauthorizedException ||
                    exception is HttpResponseInternalServerErrorException ||
                    exception is HttpResponseException)
                {
                    throw LoggingUtilities.CreateAndLogException<DetectionDependencyException>(_logger, new FailedDetectionDependencyException(exception));
                }


                // If the exception is any other type, rethrow it as a DetectionServiceException with a FailedDetectionServiceException as the inner exception
                throw LoggingUtilities.CreateAndLogException<DetectionServiceException>(_logger, new FailedDetectionServiceException(exception));
            }
        }
    }
}
