using Moq;
using OrcaHello.Web.Api.Models;
using static OrcaHello.Web.Api.Services.CommentOrchestrationService;

namespace OrcaHello.Web.Api.Tests.Unit.Services
{
    public partial class CommentOrchestrationTests
    {
        [TestMethod]
        public void TryCatch_CommentListResponse_Expect_Exception()
        {
            var wrapper = new CommentOrchestrationServiceWrapper();
            var delegateMock = new Mock<ReturningCommentListResponseFunction>();

            delegateMock
               .SetupSequence(p => p())

           .Throws(new InvalidCommentOrchestrationException())

           .Throws(new MetadataValidationException())
           .Throws(new MetadataDependencyValidationException())

           .Throws(new MetadataDependencyException())
           .Throws(new MetadataServiceException())

           .Throws(new Exception());

            Assert.ThrowsExceptionAsync<CommentOrchestrationValidationException>(async () =>
                await wrapper.TryCatch(delegateMock.Object));

            for (int x = 0; x < 2; x++)
            {
                Assert.ThrowsExceptionAsync<CommentOrchestrationDependencyValidationException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            for (int x = 0; x < 2; x++)
            {
                Assert.ThrowsExceptionAsync<CommentOrchestrationDependencyException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            Assert.ThrowsExceptionAsync<CommentOrchestrationServiceException>(async () =>
                await wrapper.TryCatch(delegateMock.Object));
        }
    }
}
