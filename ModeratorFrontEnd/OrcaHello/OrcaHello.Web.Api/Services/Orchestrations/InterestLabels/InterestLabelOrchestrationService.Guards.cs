namespace OrcaHello.Web.Api.Services
{
    public partial class InterestLabelOrchestrationService
    {
        protected void Validate(string propertyValue, string propertyName)
        {
            if (ValidatorUtilities.IsInvalid(propertyValue))
            {
                throw new InvalidInterestLabelOrchestrationException(LoggingUtilities.MissingRequiredProperty(propertyName));
            }
        }

        protected void ValidateMetadataFound(Metadata metadata, string id)
        {
            if (metadata is null)
            {
                throw new NotFoundMetadataException(id);
            }
        }
        protected void ValidateDeleted(bool deleted, string id)
        {
            if (!deleted)
            {
                throw new DetectionNotDeletedException(id);
            }
        }

        protected void ValidateInserted(bool inserted, string id)
        {
            if (!inserted)
            {
                throw new DetectionNotInsertedException(id);
            }
        }
    }
}
