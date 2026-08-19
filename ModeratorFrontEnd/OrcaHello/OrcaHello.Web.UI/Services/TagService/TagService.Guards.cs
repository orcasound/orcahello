namespace OrcaHello.Web.UI.Services
{
    /// <summary>
    /// Partial of the the <see cref="TagService"/> foundation service class responsible for peforming 
    /// level-specific validations.
    /// </summary>
    public partial class TagService
    {
        // RULE: String property must be present.
        protected void Validate(string propertyValue, string propertyName)
        {
            if (ValidatorUtilities.IsInvalid(propertyValue))
            {
                throw new InvalidTagException(LoggingUtilities.MissingRequiredProperty(propertyName));
            }
        }

        // RULE: Date range must be valid.
        protected void ValidateDateRange(DateTime? fromDate, DateTime? toDate)
        {
            if (!fromDate.HasValue)
            {
                throw new InvalidTagException("Property 'fromDate' cannot be null.");
            }

            if (fromDate.Value > DateTime.UtcNow)
            {
                throw new InvalidTagException("Property 'fromDate' cannot be in the future.");
            }

            if (!toDate.HasValue)
            {
                throw new InvalidTagException("Property 'toDate' cannot be null.");
            }

            if (toDate.Value < fromDate.Value)
            {
                throw new InvalidTagException("Property 'toDate' cannot be before the 'fromDate'.");
            }
        }

        // RULE: Request cannot be null.
        protected void ValidateRequest<T>(T response)
        {
            if (response == null)
            {
                throw new NullTagRequestException();
            }
        }

        // RULE: Response cannot be null.
        protected void ValidateResponse<T>(T response)
        {
            if (response == null)
            {
                throw new NullTagResponseException();
            }
        }
    }
}
