namespace OrcaHello.Web.Api.Services
{
    public partial class HydrophoneService
    {
        public delegate ValueTask<T> ReturningGenericFunction<T>();

        protected async ValueTask<T> TryCatch<T>(ReturningGenericFunction<T> returningGenericFunction)
        {
            try
            {
                return await returningGenericFunction();
            }
            catch (Exception exception)
            {
                if (exception is InvalidHydrophoneException)
                {
                    throw LoggingUtilities.CreateAndLogException<HydrophoneValidationException>(_logger, exception);
                }

                if (exception is HttpRequestException exception1)
                {
                    var statusCode = exception1.StatusCode;
                    var innerException = new InvalidHydrophoneException($"Error encountered accessing down range service defined by 'HydrophoneFeedUrl' setting: {exception1.Message}");

                    if (statusCode == HttpStatusCode.BadRequest ||
                        statusCode == HttpStatusCode.NotFound)
                    {
                        throw LoggingUtilities.CreateAndLogException<HydrophoneDependencyValidationException>(_logger, innerException);
                    }

                    if (statusCode == HttpStatusCode.Unauthorized ||
                        statusCode == HttpStatusCode.Forbidden ||
                        statusCode == HttpStatusCode.MethodNotAllowed ||
                        statusCode == HttpStatusCode.Conflict ||
                        statusCode == HttpStatusCode.PreconditionFailed ||
                        statusCode == HttpStatusCode.RequestEntityTooLarge ||
                        statusCode == HttpStatusCode.RequestTimeout ||
                        statusCode == HttpStatusCode.ServiceUnavailable ||
                        statusCode == HttpStatusCode.InternalServerError)
                    {
                        throw LoggingUtilities.CreateAndLogException<HydrophoneDependencyException>(_logger, innerException);
                    }

                    throw LoggingUtilities.CreateAndLogException<HydrophoneServiceException>(_logger, innerException);

                }

                throw LoggingUtilities.CreateAndLogException<HydrophoneServiceException>(_logger, exception);
            }
        }
    }
}
