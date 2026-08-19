using static OrcaHello.Web.Api.Services.MetadataService;

namespace OrcaHello.Web.Api.Tests.Unit.Services
{
    public partial class MetadataServiceTests
    {
        [TestMethod]
        public async Task TryCatch_ReturningGenericFunction_Expect_Exception()
        {
            var wrapper = new MetadataServiceWrapper();
            var delegateMock = new Mock<ReturningGenericFunction<MetricsSummaryForTimeframe>>();

            var cosmosErrorMessage = "{\"errors\":[{\"severity\":\"Error\",\"location\":{\"start\":22,\"end\":26},\"code\":\"SC2001\",\"message\":\"error message.\"}]}););\"";

            delegateMock
                .SetupSequence(p => p())

            .Throws(new InvalidMetadataException())

            .Throws(new CosmosException(cosmosErrorMessage, HttpStatusCode.BadRequest, 0, null, 0.0))
            .Throws(new CosmosException(cosmosErrorMessage, HttpStatusCode.NotFound, 0, null, 0.0))

            .Throws(new CosmosException(cosmosErrorMessage, HttpStatusCode.Unauthorized, 0, null, 0.0))
            .Throws(new CosmosException(cosmosErrorMessage, HttpStatusCode.Forbidden, 0, null, 0.0))
            .Throws(new CosmosException(cosmosErrorMessage, HttpStatusCode.MethodNotAllowed, 0, null, 0.0))
            .Throws(new CosmosException(cosmosErrorMessage, HttpStatusCode.Conflict, 0, null, 0.0))
            .Throws(new CosmosException(cosmosErrorMessage, HttpStatusCode.PreconditionFailed, 0, null, 0.0))
            .Throws(new CosmosException(cosmosErrorMessage, HttpStatusCode.RequestEntityTooLarge, 0, null, 0.0))
            .Throws(new CosmosException(cosmosErrorMessage, HttpStatusCode.RequestTimeout, 0, null, 0.0))
            .Throws(new CosmosException(cosmosErrorMessage, HttpStatusCode.ServiceUnavailable, 0, null, 0.0))
            .Throws(new CosmosException(cosmosErrorMessage, HttpStatusCode.InternalServerError, 0, null, 0.0))

            .Throws(new CosmosException(cosmosErrorMessage, HttpStatusCode.AlreadyReported, 0, null, 0.0))

            .Throws(new ArgumentNullException())
            .Throws(new ArgumentException())
            .Throws(new HttpRequestException())
            .Throws(new AggregateException())
            .Throws(new InvalidOperationException())

            .Throws(new Exception());

            await Assert.ThrowsExceptionAsync<MetadataValidationException>(async () =>
                await wrapper.TryCatch(delegateMock.Object));

            for (int x = 0; x < 2; x++)
            {
                await Assert.ThrowsExceptionAsync<MetadataDependencyValidationException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            for (int x = 0; x < 9; x++)
            {
                await Assert.ThrowsExceptionAsync<MetadataDependencyException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            await Assert.ThrowsExceptionAsync<MetadataServiceException>(async () =>
                await wrapper.TryCatch(delegateMock.Object));

            for (int x = 0; x < 5; x++)
            {
                await Assert.ThrowsExceptionAsync<MetadataDependencyException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            await Assert.ThrowsExceptionAsync<MetadataServiceException>(async () =>
                await wrapper.TryCatch(delegateMock.Object));
        }
    }
}
