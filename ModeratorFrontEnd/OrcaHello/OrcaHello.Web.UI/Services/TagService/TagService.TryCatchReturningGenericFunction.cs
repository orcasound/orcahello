namespace OrcaHello.Web.UI.Services
{
    /// <summary>
    /// Partial of the the <see cref="TagService"/> foundation service class responsible for peforming a generic
    /// TryCatch to marshal level-specific and dependent exceptions.
    /// </summary>
    public partial class TagService
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
                if (exception is InvalidTagException ||
                    exception is NullTagRequestException ||
                    exception is NullTagResponseException)
                {
                    throw LoggingUtilities.CreateAndLogException<TagValidationException>(_logger, exception);
                }

                // If the exception is an HttpResponseConflictException, rethrow it as a TagDependencyValidationException with an AlreadyExistsTagException as the inner exception
                if (exception is HttpResponseConflictException)
                {
                    throw LoggingUtilities.CreateAndLogException<TagDependencyValidationException>(_logger, new AlreadyExistsTagException(exception));
                }

                // If the exception is an HttpResponseBadRequestException, rethrow it as a TagDependencyValidationException with an InvalidTagException as the inner exception
                if (exception is HttpResponseBadRequestException)
                {
                    throw LoggingUtilities.CreateAndLogException<TagDependencyValidationException>(_logger, new InvalidTagException(exception));
                }

                // If the exception is any of the following types, rethrow it as a TagDependencyException with a FailedTagDependencyException as the inner exception
                if (exception is HttpRequestException ||
                    exception is HttpResponseUrlNotFoundException ||
                    exception is HttpResponseUnauthorizedException ||
                    exception is HttpResponseInternalServerErrorException ||
                    exception is HttpResponseException)
                {
                    throw LoggingUtilities.CreateAndLogException<TagDependencyException>(_logger, new FailedTagDependencyException(exception));
                }

                // If the exception is any other type, rethrow it as a TagServiceException with a FailedTagServiceException as the inner exception
                throw LoggingUtilities.CreateAndLogException<TagServiceException>(_logger, new FailedTagServiceException(exception));
            }
        }
    }
}
