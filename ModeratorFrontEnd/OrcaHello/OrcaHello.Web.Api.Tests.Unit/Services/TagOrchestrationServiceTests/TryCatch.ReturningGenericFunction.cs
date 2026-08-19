using static OrcaHello.Web.Api.Services.TagOrchestrationService;

namespace OrcaHello.Web.Api.Tests.Unit.Services
{
    public partial class TagOrchestrationServiceTests
    {
        [TestMethod]
        public void TryCatch_ReturningGenericFunction_Expect_Exception()
        {
            var wrapper = new TagOrchestrationServiceWrapper();
            var delegateMock = new Mock<ReturningGenericFunction<TagListResponse>>();

            delegateMock
               .SetupSequence(p => p())

           .Throws(new InvalidTagOrchestrationException())

           .Throws(new MetadataValidationException())
           .Throws(new MetadataDependencyValidationException())

           .Throws(new MetadataDependencyException())
           .Throws(new MetadataServiceException())

           .Throws(new Exception());

            Assert.ThrowsExceptionAsync<TagOrchestrationValidationException>(async () =>
                await wrapper.TryCatch(delegateMock.Object));

            for (int x = 0; x < 2; x++)
            {
                Assert.ThrowsExceptionAsync<TagOrchestrationDependencyValidationException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            for (int x = 0; x < 2; x++)
            {
                Assert.ThrowsExceptionAsync<TagOrchestrationDependencyException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            Assert.ThrowsExceptionAsync<TagOrchestrationServiceException>(async () =>
                await wrapper.TryCatch(delegateMock.Object));
        }
    }
}