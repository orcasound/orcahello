namespace OrcaHello.Web.Api.Services
{
    public partial class CommentOrchestrationService
    {
        protected void Validate(DateTime? date, string propertyName)
        {
            if (!date.HasValue || ValidatorUtilities.IsInvalid(date.Value))
            {
                throw new InvalidCommentOrchestrationException(LoggingUtilities.MissingRequiredProperty(propertyName));
            }
        }

        protected void ValidatePage(int page)
        {
            if (ValidatorUtilities.IsZeroOrLess(page))
            {
                throw new InvalidCommentOrchestrationException(LoggingUtilities.InvalidProperty("page"));
            }
        }

        protected void ValidatePageSize(int pageSize)
        {
            if (ValidatorUtilities.IsZeroOrLess(pageSize))
            {
                throw new InvalidCommentOrchestrationException(LoggingUtilities.InvalidProperty("pageSize"));
            }
        }
    }
}
