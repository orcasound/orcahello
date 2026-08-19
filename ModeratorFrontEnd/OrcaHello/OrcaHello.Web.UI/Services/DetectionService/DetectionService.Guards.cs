namespace OrcaHello.Web.UI.Services
{
    /// <summary>
    /// Partial of the the <see cref="DetectionService"/> foundation service class responsible for peforming 
    /// level-specific validations.
    /// </summary>
    public partial class DetectionService
    {
        // RULE: String property must be present.
        protected void Validate(string propertyValue, string propertyName)
        {
            if (ValidatorUtilities.IsInvalid(propertyValue))
            {
                throw new InvalidDetectionException(LoggingUtilities.MissingRequiredProperty(propertyName));
            }
        }

        // RULE: Date must be present and valid
        protected void Validate(DateTime? date, string propertyName)
        {
            if (!date.HasValue || ValidatorUtilities.IsInvalid(date.Value))
            {
                throw new InvalidDetectionException(LoggingUtilities.MissingRequiredProperty(propertyName));
            }
        }

        // RULE: Must be at least one valid id and all ids must be a non-empty string
        protected void ValidateAtLeastOneId(List<string> items, string propertyName)
        {
            if (items == null || !items.Any())
            {
                throw new InvalidDetectionException($"Property '{propertyName}' must not be null and contain at least one value.");
            }

            foreach (var item in items)
            {
                if (ValidatorUtilities.IsInvalid(item))
                {
                    throw new InvalidDetectionException($"Property '{propertyName}' contains at least one empty or non-string value.");
                }
            }
        }

        // RULE: Date range must be valid.
        protected void ValidateDateRange(DateTime? fromDate, DateTime? toDate)
        {
            if (!fromDate.HasValue)
            {
                throw new InvalidDetectionException("Property 'fromDate' cannot be null.");
            }

            if (fromDate.Value > DateTime.UtcNow)
            {
                throw new InvalidDetectionException("Property 'fromDate' cannot be in the future.");
            }

            if (!toDate.HasValue)
            {
                throw new InvalidDetectionException("Property 'toDate' cannot be null.");
            }

            if (toDate.Value < fromDate.Value)
            {
                throw new InvalidDetectionException("Property 'toDate' cannot be before the 'fromDate'.");
            }
        }

        // RULE: Pagination must be correct.
        protected void ValidatePagination(int page, int pageSize)
        {
            if (page <= 0)
            {
                throw new InvalidDetectionException("Property 'page' number must be positive.");
            }

            if (pageSize <= 0)
            {
                throw new InvalidDetectionException("Property 'pageSize' number must be positive.");
            }
        }

        // RULE: Request cannot be null.
        protected void ValidateRequest<T>(T response)
        {
            if (response == null)
            {
                throw new NullDetectionRequestException();
            }
        }

        // RULE: Response cannot be null.
        protected void ValidateResponse<T>(T response)
        {
            if (response == null)
            {
                throw new NullDetectionResponseException();
            }
        }
    }
}
