namespace OrcaHello.Web.UI.Services
{
    /// <summary>
    /// Partial of the the <see cref="DetectionViewService"/> orchestration service class responsible for peforming a generic
    /// TryCatch to marshal level-specific and dependent exceptions.
    /// </summary>
    public partial class DetectionViewService
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
                // If the exception is one of the following types, rethrow it as a DetectionViewValidationException and log it.
                // These exceptions indicate that there is something wrong with the view service itself or the request
                // or response objects.
                if (exception is InvalidDetectionViewException ||
                    exception is NullDetectionViewRequestException ||
                    exception is NullDetectionViewResponseException)
                {
                    throw LoggingUtilities.CreateAndLogException<DetectionViewValidationException>(_logger, exception);
                }

                // If the exception is one of the following types, rethrow it as a DetectionViewDependencyValidationException and log it.
                // These exceptions indicate that there is something wrong with the validation of the Tag entity or its dependencies.
                if (exception is DetectionValidationException ||
                    exception is TagValidationException ||
                    exception is DetectionDependencyValidationException ||
                    exception is TagDependencyValidationException)
                {
                    throw LoggingUtilities.CreateAndLogException<DetectionViewDependencyValidationException>(_logger, exception);
                }

                // If the exception is one of the following types, rethrow it as a DetectionViewDependencyException and log it.
                // These exceptions indicate that there is something wrong with the dependency services or the communication with them.
                if (exception is DetectionDependencyException ||
                    exception is TagDependencyException ||
                    exception is DetectionServiceException ||
                    exception is TagServiceException)
                {
                    throw LoggingUtilities.CreateAndLogException<DetectionViewDependencyException>(_logger, exception);
                }

                // If the exception is any other type, rethrow it as a DetectionViewServiceException and log it.
                // This is a generic exception that indicates that something unexpected happened in the view service.
                throw LoggingUtilities.CreateAndLogException<DetectionViewServiceException>(_logger, exception);
            }
        }
    }
}