using static OrcaHello.Web.UI.Services.ModeratorService;

namespace OrcaHello.Web.UI.Tests.Unit.Services
{
    public partial class ModeratorServiceTests
    {
        [TestMethod]
        public void TryCatch_ReturningGenericFunction_Expect_Exception()
        {
            var wrapper = new ModeratorServiceWrapper();
            var delegateMock = new Mock<ReturningGenericFunction<CommentListForModeratorResponse>>();

            delegateMock
                .SetupSequence(p => p())

            .Throws(new InvalidModeratorException())
            .Throws(new NullModeratorResponseException())

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
                Assert.ThrowsExceptionAsync<ModeratorValidationException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            for (int x = 0; x < 2; x++)
            {
                Assert.ThrowsExceptionAsync<ModeratorDependencyValidationException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            for (int x = 0; x < 5; x++)
            {
                Assert.ThrowsExceptionAsync<ModeratorDependencyException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            Assert.ThrowsExceptionAsync<ModeratorServiceException>(async () =>
                await wrapper.TryCatch(delegateMock.Object));
        }
    }
}
