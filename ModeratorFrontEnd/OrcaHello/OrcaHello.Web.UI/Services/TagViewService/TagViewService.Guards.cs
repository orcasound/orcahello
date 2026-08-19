namespace OrcaHello.Web.UI.Services
{
    /// <summary>
    /// Partial of the the <see cref="TagViewService"/> orchestration service class responsible for peforming 
    /// level-specific validations.
    /// </summary>
    public partial class TagViewService
    {
        protected void ValidateAtLeastOneTagSelected(List<string> tags)
        {
            if (tags == null || !tags.Any())
            {
                throw new InvalidTagViewException("At least one 'Tag' must be selected.");
            }
        }

        // RULE: Date range must be valid.
        protected void ValidateDateRange(DateTime? fromDate, DateTime? toDate)
        {
            if (!fromDate.HasValue)
            {
                throw new InvalidTagViewException("The 'Start' date cannot be null.");
            }

            if (fromDate.Value > DateTime.UtcNow)
            {
                throw new InvalidTagViewException("The 'Start' date cannot be in the future.");
            }

            if (!toDate.HasValue)
            {
                throw new InvalidTagViewException("The 'End' date cannot be null.");
            }

            if (toDate.Value < fromDate.Value)
            {
                throw new InvalidTagViewException("The 'End' date cannot be before the 'Start' date.");
            }
        }

        // RULE: Pagination must be correct.
        protected void ValidatePagination(int page, int pageSize)
        {
            if (page <= 0)
            {
                throw new InvalidTagViewException("The page number must be positive.");
            }

            if (pageSize <= 0)
            {
                throw new InvalidTagViewException("The page size must be positive.");
            }
        }

        // RULE: Tag cannot be null, empty, or whitespace.
        protected void ValidateTagString(string tag, string name)
        {
            if (String.IsNullOrWhiteSpace(tag))
            {
                throw new InvalidTagViewException($"{name} cannot be null, empty, or whitespace.");
            }
        }

        // RULE: Request cannot be null.
        protected void ValidateRequest<T>(T request)
        {
            if (request == null)
            {
                throw new NullTagViewRequestException(nameof(T));
            }
        }

        // RULE: Response cannot be null.
        protected void ValidateResponse<T>(T response)
        {
            if (response == null)
            {
                throw new NullTagViewResponseException(nameof(T));
            }
        }
    }
}
