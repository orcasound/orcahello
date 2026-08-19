using static OrcaHello.Web.UI.Services.DetectionService;

namespace OrcaHello.Web.UI.Tests.Unit.Services
{
    public partial class DetectionServiceTests
    {
        [TestMethod]
        public async Task TryCatch_ReturningGenericFunction_Expect_Exception()
        {
            var wrapper = new DetectionServiceWrapper();
            var delegateMock = new Mock<ReturningGenericFunction<DetectionListResponse>>();

            delegateMock
                .SetupSequence(p => p())

            .Throws(new InvalidDetectionException())
            .Throws(new NullDetectionRequestException())
            .Throws(new NullDetectionResponseException())

            .Throws(new HttpResponseConflictException())
            .Throws(new HttpResponseBadRequestException())

            .Throws(new HttpRequestException())
            .Throws(new HttpResponseUrlNotFoundException())
            .Throws(new HttpResponseUnauthorizedException())
            .Throws(new HttpResponseInternalServerErrorException())
            .Throws(new HttpResponseException())

            .Throws(new Exception());

            for (int x = 0; x < 3; x++)
            {
                await Assert.ThrowsExceptionAsync<DetectionValidationException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            for (int x = 0; x < 2; x++)
            {
                await Assert.ThrowsExceptionAsync<DetectionDependencyValidationException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            for (int x = 0; x < 5; x++)
            {
                await Assert.ThrowsExceptionAsync<DetectionDependencyException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            await Assert.ThrowsExceptionAsync<DetectionServiceException>(async () =>
                await wrapper.TryCatch(delegateMock.Object));
        }
    }
}
