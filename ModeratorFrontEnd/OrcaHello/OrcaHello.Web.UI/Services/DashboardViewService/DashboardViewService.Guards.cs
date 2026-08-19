namespace OrcaHello.Web.UI.Services
{
    /// <summary>
    /// Partial of the the <see cref="DashboardViewService"/> orchestration service class responsible for peforming 
    /// level-specific validations.
    /// </summary>
    public partial class DashboardViewService
    {
        // RULE: Date range must be valid.
        protected void ValidateDateRange(DateTime? fromDate, DateTime? toDate)
        {
            if (!fromDate.HasValue)
            {
                throw new InvalidDashboardViewException("The 'Start' date cannot be null.");
            }

            if (fromDate.Value > DateTime.UtcNow)
            {
                throw new InvalidDashboardViewException("The 'Start' date cannot be in the future.");
            }

            if (!toDate.HasValue)
            {
                throw new InvalidDashboardViewException("The 'End' date cannot be null.");
            }

            if (toDate.Value < fromDate.Value)
            {
                throw new InvalidDashboardViewException("The 'End' date cannot be before the 'Start' date.");
            }
        }

        // RULE: Pagination must be correct.
        protected void ValidatePagination(int page, int pageSize)
        {
            if (page <= 0)
            {
                throw new InvalidDashboardViewException("The page number must be positive.");
            }

            // If the pageSize parameter is less than or equal to zero, throw an InvalidDashboardViewException with a custom message.
            if (pageSize <= 0)
            {
                throw new InvalidDashboardViewException("The page size must be positive.");
            }
        }

        // RULE: Check if the moderator name is valid.
        protected void ValidateModerator(string moderator)
        {
            if (String.IsNullOrWhiteSpace(moderator))
            {
                throw new InvalidDashboardViewException("The moderator name cannot be null, empty, or whitespace.");
            }
        }

        // RULE: Check if the tag is valid.
        protected void ValidateTag(string tag)
        {
            if (String.IsNullOrWhiteSpace(tag))
            {
                throw new InvalidDashboardViewException("The tag cannot be null, empty, or whitespace.");
            }
        }

        // RULE: Request cannot be null.
        protected void ValidateRequest<T>(T request)
        {
            if (request == null)
            {
                throw new NullDashboardViewRequestException(nameof(T));
            }
        }

        // RULE: Response cannot be null.
        protected void ValidateResponse<T>(T response)
        {
            if (response == null)
            {
                throw new NullDashboardViewResponseException(nameof(T));
            }
        }
    }
}
