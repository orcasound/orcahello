namespace OrcaHello.Web.UI.Services
{
    public partial class DetectionViewService
    {
        // RULE: String cannot be null, empty, or whitespace.
        protected void Validate(string property, string propertyName)
        {
            if (String.IsNullOrWhiteSpace(property))
                throw new InvalidDetectionViewException($"{propertyName} cannot be null, empty, or whitespace.");
        }

        // RULE: At least one Id must be provided and no value in the list can be null, empty, or whitespace
        protected void ValidateAtLeastOneId(List<string> ids)
        {
            if (ids == null || !ids.Any())
                throw new InvalidDetectionViewException("At least one 'Id' must be provided.");

            foreach (var id in ids)
            {
                if (String.IsNullOrWhiteSpace(id))
                    throw new InvalidDetectionViewException("At least one 'Id' is either null, empty, or whitespace.");
            }
        }

        // RULE: Date range must be valid.
        protected void ValidateDateRange(DateTime? fromDate, DateTime? toDate)
        {
            if (!fromDate.HasValue)
                throw new InvalidDetectionViewException("The 'Start' date cannot be null.");

            if (fromDate.Value > DateTime.UtcNow)
                throw new InvalidDetectionViewException("The 'Start' date cannot be in the future.");

            if (!toDate.HasValue)
                throw new InvalidDetectionViewException("The 'End' date cannot be null.");

            if (toDate.Value < fromDate.Value)
                throw new InvalidDetectionViewException("The 'End' date cannot be before the 'Start' date.");
        }


        // RULE: Pagination must be correct.
        protected void ValidatePagination(int page, int pageSize)
        {
            if (page <= 0)
                throw new InvalidDetectionViewException("The page number must be positive.");

            if (pageSize <= 0)
                throw new InvalidDetectionViewException("The page size must be positive.");
        }

        // RULE: Request cannot be null.
        protected void ValidateRequest<T>(T request)
        {
            if (request == null)
            {
                throw new NullDetectionViewRequestException(nameof(T));
            }
        }

        // RULE: Response cannot be null.
        protected void ValidateResponse<T>(T response)
        {
            if (response == null)
            {
                throw new NullDetectionViewResponseException(nameof(T));
            }
        }
    }
}
