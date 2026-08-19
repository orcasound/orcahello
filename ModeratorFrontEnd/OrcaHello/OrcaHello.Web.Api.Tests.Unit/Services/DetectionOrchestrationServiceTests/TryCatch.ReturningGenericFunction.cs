using static OrcaHello.Web.Api.Services.DetectionOrchestrationService;

namespace OrcaHello.Web.Api.Tests.Unit.Services
{
    public partial class DetectionOrchestrationServiceTests
    {
        [TestMethod]
        public async Task TryCatch_ReturningGenericFunction_Expect_Exception()
        {
            var wrapper = new DetectionOrchestrationServiceWrapper();
            var delegateMock = new Mock<ReturningGenericFunction<DetectionListResponse>>();

            delegateMock
               .SetupSequence(p => p())

           .Throws(new NotFoundMetadataException("id"))
           .Throws(new InvalidDetectionOrchestrationException())

           .Throws(new MetadataValidationException())
           .Throws(new MetadataDependencyValidationException())

           .Throws(new MetadataDependencyException())
           .Throws(new MetadataServiceException())

           .Throws(new Exception());

            for (int x = 0; x < 2; x++)
            {
                await Assert.ThrowsExceptionAsync<DetectionOrchestrationValidationException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            for (int x = 0; x < 2; x++)
            {
                await Assert.ThrowsExceptionAsync<DetectionOrchestrationDependencyValidationException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            for (int x = 0; x < 2; x++)
            {
                await Assert.ThrowsExceptionAsync<DetectionOrchestrationDependencyException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            await Assert.ThrowsExceptionAsync<DetectionOrchestrationServiceException>(async () =>
                await wrapper.TryCatch(delegateMock.Object));
        }
    }
}
