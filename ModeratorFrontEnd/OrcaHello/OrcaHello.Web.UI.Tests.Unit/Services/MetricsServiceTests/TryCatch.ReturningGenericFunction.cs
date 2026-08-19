using static OrcaHello.Web.UI.Services.MetricsService;

namespace OrcaHello.Web.UI.Tests.Unit.Services
{
    public partial class MetricsServiceTest
    {
        [TestMethod]
        public async Task TryCatch_ReturningGenericFunction_Expect_Exception()
        {
            var wrapper = new MetricsServiceWrapper();
            var delegateMock = new Mock<ReturningGenericFunction<MetricsResponse>>();

            delegateMock
                .SetupSequence(p => p())

            .Throws(new InvalidMetricsException())
            .Throws(new NullMetricsResponseException())

            .Throws(new HttpResponseConflictException())
            .Throws(new HttpResponseBadRequestException())

            .Throws(new HttpRequestException())
            .Throws(new HttpResponseUrlNotFoundException())
            .Throws(new HttpResponseUnauthorizedException())
            .Throws(new HttpResponseInternalServerErrorException())
            .Throws(new HttpResponseException())

            .Throws(new Exception());

            for (int x = 0; x < 2; x++)
            {
                await Assert.ThrowsExceptionAsync<MetricsValidationException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            for (int x = 0; x < 2; x++)
            {
                await Assert.ThrowsExceptionAsync<MetricsDependencyValidationException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            for (int x = 0; x < 5; x++)
            {
                await Assert.ThrowsExceptionAsync<MetricsDependencyException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            await Assert.ThrowsExceptionAsync<MetricsServiceException>(async () =>
                await wrapper.TryCatch(delegateMock.Object));
        }
    }
}
