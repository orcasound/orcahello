namespace OrcaHello.Web.UI.Services
{
    /// <summary>
    /// Partial of the the <see cref="ModeratorService"/> foundation service class responsible for peforming 
    /// level-specific validations.
    /// </summary>
    public partial class ModeratorService
    {
        // RULE: String property must be present.
        protected void Validate(string propertyValue, string propertyName)
        {
            if (ValidatorUtilities.IsInvalid(propertyValue))
            {
                throw new InvalidModeratorException(LoggingUtilities.MissingRequiredProperty(propertyName));
            }
        }

        // RULE: Date range must be valid.
        protected void ValidateDateRange(DateTime? fromDate, DateTime? toDate)
        {
            if (!fromDate.HasValue)
            {
                throw new InvalidModeratorException("Property 'fromDate' cannot be null.");
            }

            if (fromDate.Value > DateTime.UtcNow)
            {
                throw new InvalidModeratorException("Property 'fromDate' cannot be in the future.");
            }

            if (!toDate.HasValue)
            {
                throw new InvalidModeratorException("Property 'toDate' cannot be null.");
            }

            if (toDate.Value < fromDate.Value)
            {
                throw new InvalidModeratorException("Property 'toDate' cannot be before the 'fromDate'.");
            }
        }

        // RULE: Pagination must be correct.
        protected void ValidatePagination(int page, int pageSize)
        {
            if (page <= 0)
            {
                throw new InvalidModeratorException("Property 'page' number must be positive.");
            }

            if (pageSize <= 0)
            {
                throw new InvalidModeratorException("Property 'pageSize' number must be positive.");
            }
        }

        // RULE: Response cannot be null.
        protected void ValidateResponse<T>(T response)
        {
            if (response == null)
            {
                throw new NullModeratorResponseException();
            }
        }
    }
}
