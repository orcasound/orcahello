namespace OrcaHello.Web.Api.Services
{
    public partial class MetadataService
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
                if (exception is InvalidMetadataException ||
                    exception is NullMetadataException)
                {
                    throw LoggingUtilities.CreateAndLogException<MetadataValidationException>(_logger, exception);
                }

                if (exception is CosmosException)
                {
                    var statusCode = CosmosUtilities.GetHttpStatusCode(exception);
                    var cosmosReason = CosmosUtilities.GetReason(exception);

                    var innerException = new InvalidMetadataException(cosmosReason);

                    if (statusCode == HttpStatusCode.BadRequest ||
                        statusCode == HttpStatusCode.NotFound)
                    {
                        throw LoggingUtilities.CreateAndLogException<MetadataDependencyValidationException>(_logger, innerException);
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
                        throw LoggingUtilities.CreateAndLogException<MetadataDependencyException>(_logger, innerException);
                    }

                    throw LoggingUtilities.CreateAndLogException<MetadataServiceException>(_logger, innerException);
                }

                if (exception is ArgumentNullException ||
                    exception is ArgumentException ||
                    exception is HttpRequestException ||
                    exception is AggregateException ||
                    exception is InvalidOperationException)
                {
                    throw LoggingUtilities.CreateAndLogException<MetadataDependencyException>(_logger, exception);
                }

                throw LoggingUtilities.CreateAndLogException<MetadataServiceException>(_logger, exception);
            }
        }

    }
}
