using Moq;
using OrcaHello.Web.Api.Models;
using OrcaHello.Web.Shared.Models.Moderators;
using static OrcaHello.Web.Api.Services.ModeratorOrchestrationService;

namespace OrcaHello.Web.Api.Tests.Unit.Services
{
    public partial class ModeratorOrchestrationServiceTests
    {
        [TestMethod]
        public void TryCatch_GenericResponse_Expect_Exception()
        {
            var wrapper = new ModeratorOrchestrationServiceWrapper();
            var delegateMock = new Mock<ReturningGenericFunction<MetricsForModeratorResponse>>();

            delegateMock
               .SetupSequence(p => p())

           .Throws(new InvalidModeratorOrchestrationException())

           .Throws(new MetadataValidationException())
           .Throws(new MetadataDependencyValidationException())

           .Throws(new MetadataDependencyException())
           .Throws(new MetadataServiceException())

           .Throws(new Exception());

            Assert.ThrowsExceptionAsync<ModeratorOrchestrationValidationException>(async () =>
                await wrapper.TryCatch(delegateMock.Object));

            for (int x = 0; x < 2; x++)
            {
                Assert.ThrowsExceptionAsync<ModeratorOrchestrationDependencyValidationException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            for (int x = 0; x < 2; x++)
            {
                Assert.ThrowsExceptionAsync<ModeratorOrchestrationDependencyException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            Assert.ThrowsExceptionAsync<ModeratorOrchestrationServiceException>(async () =>
                await wrapper.TryCatch(delegateMock.Object));
        }
    }
}
