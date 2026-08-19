namespace OrcaHello.Web.UI.Services
{
    /// <summary>
    /// Partial of the the <see cref="CommentService"/> foundation service class responsible for peforming a generic
    /// TryCatch to marshal level-specific and dependent exceptions.
    /// </summary>
    public partial class CommentService
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
                // If the exception is related to invalid or null comments, throw a CommentValidationException
                if (exception is InvalidCommentException ||
                    exception is NullCommentResponseException)
                {
                    throw LoggingUtilities.CreateAndLogException<CommentValidationException>(_logger, exception);
                }

                // If the exception is related to a conflict in the comment API, throw a CommentDependencyValidationException with an AlreadyExistsCommentException
                if (exception is HttpResponseConflictException)
                {
                    throw LoggingUtilities.CreateAndLogException<CommentDependencyValidationException>(_logger, new AlreadyExistsCommentException(exception));
                }

                // If the exception is related to a bad request in the comment API, throw a CommentDependencyValidationException with an InvalidCommentException
                if (exception is HttpResponseBadRequestException)
                {
                    throw LoggingUtilities.CreateAndLogException<CommentDependencyValidationException>(_logger, new InvalidCommentException(exception));
                }

                // If the exception is related to any other HTTP error in the comment API, throw a CommentDependencyException with a FailedCommentDependencyException
                if (exception is HttpRequestException ||
                    exception is HttpResponseUrlNotFoundException ||
                    exception is HttpResponseUnauthorizedException ||
                    exception is HttpResponseInternalServerErrorException ||
                    exception is HttpResponseException)
                {
                    throw LoggingUtilities.CreateAndLogException<CommentDependencyException>(_logger, new FailedCommentDependencyException(exception));
                }

                // If the exception is not handled by any of the above cases, throw a CommentServiceException with a FailedCommentServiceException
                throw LoggingUtilities.CreateAndLogException<CommentServiceException>(_logger, new FailedCommentServiceException(exception));
            }

        }
    }
}
