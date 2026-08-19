using System.Text.RegularExpressions;

namespace OrcaHello.Web.Api.Services
{
    public partial class MetadataService
    {
        protected void Validate(DateTime date, string propertyName)
        {
            if (ValidatorUtilities.IsInvalid(date))
            {
                throw new InvalidMetadataException(LoggingUtilities.MissingRequiredProperty(propertyName));
            }
        }

        protected void Validate(string propertyValue, string propertyName)
        {
            if (ValidatorUtilities.IsInvalid(propertyValue))
            {
                throw new InvalidMetadataException(LoggingUtilities.MissingRequiredProperty(propertyName));
            }
        }

        protected void ValidateDatesAreWithinRange(DateTime fromDate, DateTime toDate)
        {
            if (toDate < fromDate)
            {
                throw new InvalidMetadataException("'toDate' must be after 'fromDate'.");
            }
        }

        protected void ValidateMetadataOnCreate(Metadata metadata)
        {
            ValidateMetadataIsNotNull(metadata);

            // TODO: Are there any other required fields

            switch (metadata)
            {
                case { } when ValidatorUtilities.IsInvalid(metadata.Id):
                    throw new InvalidMetadataException(LoggingUtilities.MissingRequiredProperty(nameof(metadata.Id)));

                case { } when ValidatorUtilities.IsInvalid(metadata.State):
                    throw new InvalidMetadataException(LoggingUtilities.MissingRequiredProperty(nameof(metadata.State)));

                case { } when ValidatorUtilities.IsInvalid(metadata.LocationName):
                    throw new InvalidMetadataException(LoggingUtilities.MissingRequiredProperty(nameof(metadata.LocationName)));
            }
        }

        protected void ValidateMetadataOnUpdate(Metadata metadata)
        {
            ValidateMetadataIsNotNull(metadata);

            // TODO: Are there any other required fields

            switch (metadata)
            {
                case { } when ValidatorUtilities.IsInvalid(metadata.Id):
                    throw new InvalidMetadataException(LoggingUtilities.MissingRequiredProperty(nameof(metadata.Id)));

                case { } when ValidatorUtilities.IsInvalid(metadata.State):
                    throw new InvalidMetadataException(LoggingUtilities.MissingRequiredProperty(nameof(metadata.State)));

                case { } when ValidatorUtilities.IsInvalid(metadata.LocationName):
                    throw new InvalidMetadataException(LoggingUtilities.MissingRequiredProperty(nameof(metadata.LocationName)));
            }
        }

        private static void ValidateMetadataIsNotNull(Metadata metadata)
        {
            if (metadata is null)
            {
                throw new NullMetadataException();
            }
        }

        protected void ValidateStateIsAcceptable(string state)
        {
            if (ValidatorUtilities.GetMatchingEnumValue(state, typeof(DetectionState)) == null)
            {
                throw new InvalidMetadataException($"'{state}' is not a valid Detection state.");
            }
        }

        protected void ValidateTagContainsOnlyValidCharacters(string tag)
        {
            string pattern = "[^a-zA-Z0-9,| ?-]";
            Regex regex = new(pattern);
            bool hasInvalidChars = regex.IsMatch(tag);

            if (hasInvalidChars)
            {
                throw new InvalidMetadataException($"'{tag}' contains one or more invalid characters (use , for AND and use | for OR).");
            }

            if (tag.Contains(',') && tag.Contains('|'))
            {
                throw new InvalidMetadataException($"'{tag}' can only work on a single operator (, or |).");
            }
        }
    }
}
