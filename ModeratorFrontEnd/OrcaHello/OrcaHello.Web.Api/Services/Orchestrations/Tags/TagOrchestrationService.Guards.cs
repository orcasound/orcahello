namespace OrcaHello.Web.Api.Services
{
    public partial class TagOrchestrationService
    {
        protected void Validate(DateTime? date, string propertyName)
        {
            if (!date.HasValue || ValidatorUtilities.IsInvalid(date.Value))
            {
                throw new InvalidTagOrchestrationException(LoggingUtilities.MissingRequiredProperty(propertyName));
            }
        }

        protected void Validate(string propertyValue, string propertyName)
        {
            if (ValidatorUtilities.IsInvalid(propertyValue))
            {
                throw new InvalidTagOrchestrationException(LoggingUtilities.MissingRequiredProperty(propertyName));
            }
        }
    }
}
