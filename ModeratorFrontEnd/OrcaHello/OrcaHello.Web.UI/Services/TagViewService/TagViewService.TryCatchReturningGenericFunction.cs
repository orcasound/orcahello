namespace OrcaHello.Web.UI.Services
{
    /// <summary>
    /// Partial of the the <see cref="TagViewService"/> orchestration service class responsible for peforming a generic
    /// TryCatch to marshal level-specific and dependent exceptions.
    /// </summary>
    public partial class TagViewService
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
                // If the exception is one of the following types, rethrow it as a TagViewValidationException and log it.
                // These exceptions indicate that there is something wrong with the view service itself or the request
                // or response objects.
                if (exception is NullTagViewRequestException ||
                    exception is InvalidTagViewException ||
                    exception is NullTagViewResponseException)
                {
                    throw LoggingUtilities.CreateAndLogException<TagViewValidationException>(_logger, exception);
                }

                // If the exception is one of the following types, rethrow it as a TagViewDependencyValidationException and log it.
                // These exceptions indicate that there is something wrong with the validation of the Tag entity or its dependencies.
                if (exception is TagValidationException ||
                    exception is DetectionValidationException ||
                    exception is TagDependencyValidationException ||
                    exception is DetectionDependencyValidationException)
                {
                    throw LoggingUtilities.CreateAndLogException<TagViewDependencyValidationException>(_logger, exception);
                }

                // If the exception is one of the following types, rethrow it as a TagViewDependencyException and log it.
                // These exceptions indicate that there is something wrong with the dependency services or the communication with them.
                if (exception is TagDependencyException ||
                    exception is DetectionDependencyException ||
                    exception is TagServiceException ||
                    exception is DetectionServiceException)
                {
                    throw LoggingUtilities.CreateAndLogException<TagViewDependencyException>(_logger, exception);
                }

                // If the exception is any other type, rethrow it as a TagViewServiceException and log it.
                // This is a generic exception that indicates that something unexpected happened in the view service.
                throw LoggingUtilities.CreateAndLogException<TagViewServiceException>(_logger, exception);
            }
        }
    }
}
