using static OrcaHello.Web.Api.Services.HydrophoneService;

namespace OrcaHello.Web.Api.Tests.Unit.Services
{
    public partial class HydrophoneServiceTests
    {
        [TestMethod]
        public async Task TryCatch_ReturningGenericFunction_Expect_Exception()
        {
            var wrapper = new HydrophoneServiceWrapper();
            var delegateMock = new Mock<ReturningGenericFunction<QueryableHydrophoneData>>();

            delegateMock
                .SetupSequence(p => p())

            .Throws(new InvalidHydrophoneException())

            .Throws(new HttpRequestException("Message", new Exception(), HttpStatusCode.BadRequest))
            .Throws(new HttpRequestException("Message", new Exception(), HttpStatusCode.NotFound))

            .Throws(new HttpRequestException("Message", new Exception(), HttpStatusCode.Unauthorized))
            .Throws(new HttpRequestException("Message", new Exception(), HttpStatusCode.Forbidden))
            .Throws(new HttpRequestException("Message", new Exception(), HttpStatusCode.MethodNotAllowed))
            .Throws(new HttpRequestException("Message", new Exception(), HttpStatusCode.Conflict))
            .Throws(new HttpRequestException("Message", new Exception(), HttpStatusCode.PreconditionFailed))
            .Throws(new HttpRequestException("Message", new Exception(), HttpStatusCode.RequestEntityTooLarge))
            .Throws(new HttpRequestException("Message", new Exception(), HttpStatusCode.RequestTimeout))
            .Throws(new HttpRequestException("Message", new Exception(), HttpStatusCode.ServiceUnavailable))
            .Throws(new HttpRequestException("Message", new Exception(), HttpStatusCode.InternalServerError))

            .Throws(new HttpRequestException("Message", new Exception(), HttpStatusCode.Ambiguous))

            .Throws(new Exception());

            await Assert.ThrowsExceptionAsync<HydrophoneValidationException>(async () =>
                await wrapper.TryCatch(delegateMock.Object));

            for (int x = 0; x < 2; x++)
            {
                await Assert.ThrowsExceptionAsync<HydrophoneDependencyValidationException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            for (int x = 0; x < 9; x++)
            {
                await Assert.ThrowsExceptionAsync<HydrophoneDependencyException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            for (int x = 0; x < 2; x++)
            {
                await Assert.ThrowsExceptionAsync<HydrophoneServiceException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }
        }
    }
}



//if (exception is HttpRequestException exception1)
//{
//    var statusCode = exception1.StatusCode;
//    var innerException = new InvalidHydrophoneException($"Error encountered accessing {_hydrophoneBroker.ApiUrl}: {exception.Message}");

//    if (statusCode == HttpStatusCode.BadRequest ||
//        statusCode == HttpStatusCode.NotFound)
//        throw LoggingUtilities.CreateAndLogException<HydrophoneDependencyValidationException>(_logger, innerException);

//    if (statusCode == HttpStatusCode.Unauthorized ||
//        statusCode == HttpStatusCode.Forbidden ||
//        statusCode == HttpStatusCode.MethodNotAllowed ||
//        statusCode == HttpStatusCode.Conflict ||
//        statusCode == HttpStatusCode.PreconditionFailed ||
//        statusCode == HttpStatusCode.RequestEntityTooLarge ||
//        statusCode == HttpStatusCode.RequestTimeout ||
//        statusCode == HttpStatusCode.ServiceUnavailable ||
//        statusCode == HttpStatusCode.InternalServerError)
//        throw LoggingUtilities.CreateAndLogException<HydrophoneDependencyException>(_logger, innerException);

//    throw LoggingUtilities.CreateAndLogException<HydrophoneServiceException>(_logger, innerException);
//}

//throw LoggingUtilities.CreateAndLogException<HydrophoneServiceException>(_logger, exception);