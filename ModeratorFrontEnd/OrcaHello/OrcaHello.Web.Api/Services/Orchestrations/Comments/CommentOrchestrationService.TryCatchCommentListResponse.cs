namespace OrcaHello.Web.Api.Services
{
    public partial class CommentOrchestrationService
    {
        public delegate ValueTask<CommentListResponse> ReturningCommentListResponseFunction();

        protected async ValueTask<CommentListResponse> TryCatch(ReturningCommentListResponseFunction returningCommentListResponseFunction)
        {
            try
            {
                return await returningCommentListResponseFunction();
            }
            catch (Exception exception)
            {
                if (exception is InvalidCommentOrchestrationException)
                {
                    throw LoggingUtilities.CreateAndLogException<CommentOrchestrationValidationException>(_logger, exception);
                }

                if (exception is MetadataValidationException ||
                    exception is MetadataDependencyValidationException)
                {
                    throw LoggingUtilities.CreateAndLogException<CommentOrchestrationDependencyValidationException>(_logger, exception);
                }

                if (exception is MetadataDependencyException ||
                    exception is MetadataServiceException)
                {
                    throw LoggingUtilities.CreateAndLogException<CommentOrchestrationDependencyException>(_logger, exception);
                }

                throw LoggingUtilities.CreateAndLogException<CommentOrchestrationServiceException>(_logger, exception);

            }
        }
    }
}
