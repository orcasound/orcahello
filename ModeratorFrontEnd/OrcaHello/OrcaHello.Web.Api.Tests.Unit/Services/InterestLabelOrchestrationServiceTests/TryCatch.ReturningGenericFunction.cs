using static OrcaHello.Web.Api.Services.InterestLabelOrchestrationService;

namespace OrcaHello.Web.Api.Tests.Unit.Services
{
    public partial class InterestLabelOrchestrationServiceTests
    {
        [TestMethod]
        public async Task TryCatch_ReturningGenericFunction_Expect_Exception()
        {
            var wrapper = new InterestLabelOrchestrationServiceWrapper();
            var delegateMock = new Mock<ReturningGenericFunction<InterestLabelListResponse>>();

            delegateMock
               .SetupSequence(p => p())

            .Throws(new NotFoundMetadataException("id"))
           .Throws(new InvalidInterestLabelOrchestrationException())

           .Throws(new MetadataValidationException())
           .Throws(new MetadataDependencyValidationException())

           .Throws(new MetadataDependencyException())
           .Throws(new MetadataServiceException())

           .Throws(new Exception());

            for (int x = 0; x < 2; x++)
            {
                await Assert.ThrowsExceptionAsync<InterestLabelOrchestrationValidationException>(async () =>
                await wrapper.TryCatch(delegateMock.Object));
            }

            for (int x = 0; x < 2; x++)
            {
                await Assert.ThrowsExceptionAsync<InterestLabelOrchestrationDependencyValidationException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            for (int x = 0; x < 2; x++)
            {
                await Assert.ThrowsExceptionAsync<InterestLabelOrchestrationDependencyException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            await Assert.ThrowsExceptionAsync<InterestLabelOrchestrationServiceException>(async () =>
                await wrapper.TryCatch(delegateMock.Object));
        }

    }
}
