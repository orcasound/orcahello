using System.Text.RegularExpressions;

namespace OrcaHello.Web.Api.Services
{
    public partial class DetectionOrchestrationService
    {
        protected void Validate(DateTime? date, string propertyName)
        {
            if (!date.HasValue || ValidatorUtilities.IsInvalid(date.Value))
            {
                throw new InvalidDetectionOrchestrationException(LoggingUtilities.MissingRequiredProperty(propertyName));
            }
        }

        protected void Validate(string propertyValue, string propertyName)
        {
            if (ValidatorUtilities.IsInvalid(propertyValue))
            {
                throw new InvalidDetectionOrchestrationException(LoggingUtilities.MissingRequiredProperty(propertyName));
            }
        }

        protected void ValidateModerateRequestOnUpdate(ModerateDetectionsRequest request)
        {
            if (request is null)
            {
                throw new NullModerateDetectionRequestException();
            }

            switch (request)
            {
                case { } when request.Ids is null || !request.Ids.Any():
                    throw new InvalidDetectionOrchestrationException(LoggingUtilities.MissingRequiredProperty(nameof(request.Ids)));

                case { } when ValidatorUtilities.IsInvalid(request.State):
                    throw new InvalidDetectionOrchestrationException(LoggingUtilities.MissingRequiredProperty(nameof(request.State)));

                case { } when ValidatorUtilities.IsInvalid(request.Moderator):
                    throw new InvalidDetectionOrchestrationException(LoggingUtilities.MissingRequiredProperty(nameof(request.Moderator)));
            }

            ValidateStateIsAcceptable(request!.State);
        }

        protected void ValidateStorageMetadata(Metadata storageMetadata, string id)
        {
            if (storageMetadata is null)
            {
                throw new NotFoundMetadataException(id);
            }
        }

        protected void ValidatePage(int page)
        {
            if (ValidatorUtilities.IsZeroOrLess(page))
            {
                throw new InvalidDetectionOrchestrationException(LoggingUtilities.InvalidProperty("page"));
            }
        }

        protected void ValidatePageSize(int pageSize)
        {
            if (ValidatorUtilities.IsZeroOrLess(pageSize))
            {
                throw new InvalidDetectionOrchestrationException(LoggingUtilities.InvalidProperty("pageSize"));
            }
        }

        protected void ValidateStateIsAcceptable(string state)
        {
            if (ValidatorUtilities.GetMatchingEnumValue(state, typeof(DetectionState)) == null)
            {
                throw new InvalidDetectionOrchestrationException($"'{state}' is not a valid Detection state.");
            }
        }
    }
}
