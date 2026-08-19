using static OrcaHello.Web.Api.Services.HydrophoneOrchestrationService;

namespace OrcaHello.Web.Api.Tests.Unit.Services
{
    public partial class HydrophoneOrchestrationServiceTests
    {
        [TestMethod]
        public void TryCatch_HydrophoneListResponse_Expect_Exception()
        {
            var wrapper = new HydrophoneOrchestrationServiceWrapper();
            var delegateMock = new Mock<ReturningGenericFunction<HydrophoneListResponse>>();

            delegateMock
               .SetupSequence(p => p())

           .Throws(new InvalidHydrophoneOrchestrationException())

           .Throws(new HydrophoneValidationException())
           .Throws(new HydrophoneDependencyValidationException())

           .Throws(new HydrophoneDependencyException())
           .Throws(new HydrophoneServiceException())

           .Throws(new Exception());

            Assert.ThrowsExceptionAsync<HydrophoneOrchestrationValidationException>(async () =>
                await wrapper.TryCatch(delegateMock.Object));

            for (int x = 0; x < 2; x++)
            {
                Assert.ThrowsExceptionAsync<HydrophoneOrchestrationDependencyValidationException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            for (int x = 0; x < 2; x++)
            {
                Assert.ThrowsExceptionAsync<HydrophoneOrchestrationDependencyException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            Assert.ThrowsExceptionAsync<HydrophoneOrchestrationServiceException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
        }
    }
};