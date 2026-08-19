using Moq;
using OrcaHello.Web.Api.Models;
using OrcaHello.Web.Shared.Models.Moderators;
using static OrcaHello.Web.Api.Services.ModeratorOrchestrationService;

namespace OrcaHello.Web.Api.Tests.Unit.Services
{
    public partial class ModeratorOrchestrationServiceTests
    {
        [TestMethod]
        public async Task TryCatch_GenericResponse_Expect_Exception()
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

            await Assert.ThrowsExceptionAsync<ModeratorOrchestrationValidationException>(async () =>
                await wrapper.TryCatch(delegateMock.Object));

            for (int x = 0; x < 2; x++)
            {
                await Assert.ThrowsExceptionAsync<ModeratorOrchestrationDependencyValidationException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            for (int x = 0; x < 2; x++)
            {
                await Assert.ThrowsExceptionAsync<ModeratorOrchestrationDependencyException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            await Assert.ThrowsExceptionAsync<ModeratorOrchestrationServiceException>(async () =>
                await wrapper.TryCatch(delegateMock.Object));
        }
    }
}
