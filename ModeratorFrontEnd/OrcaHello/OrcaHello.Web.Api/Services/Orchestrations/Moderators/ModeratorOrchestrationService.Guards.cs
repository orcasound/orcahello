namespace OrcaHello.Web.Api.Services
{
    public partial class ModeratorOrchestrationService
    {
        protected void Validate(DateTime? date, string propertyName)
        {
            if (!date.HasValue || ValidatorUtilities.IsInvalid(date.Value))
            {
                throw new InvalidModeratorOrchestrationException(LoggingUtilities.MissingRequiredProperty(propertyName));
            }
        }

        protected void Validate(string propertyValue, string propertyName)
        {
            if (ValidatorUtilities.IsInvalid(propertyValue))
            {
                throw new InvalidModeratorOrchestrationException(LoggingUtilities.MissingRequiredProperty(propertyName));
            }
        }

        protected void ValidatePage(int page)
        {
            if (ValidatorUtilities.IsZeroOrLess(page))
            {
                throw new InvalidModeratorOrchestrationException(LoggingUtilities.InvalidProperty("page"));
            }
        }

        protected void ValidatePageSize(int pageSize)
        {
            if (ValidatorUtilities.IsZeroOrLess(pageSize))
            {
                throw new InvalidModeratorOrchestrationException(LoggingUtilities.InvalidProperty("pageSize"));
            }
        }
    }
}
