using static OrcaHello.Web.UI.Services.CommentService;

namespace OrcaHello.Web.UI.Tests.Unit.Services
{
    public partial class CommentServiceTests
    {
        [TestMethod]
        public async Task TryCatch_ReturningGenericFunction_Expect_Exception()
        {
            var wrapper = new CommentServiceWrapper();
            var delegateMock = new Mock<ReturningGenericFunction<CommentListResponse>>();

            delegateMock
                .SetupSequence(p => p())

            .Throws(new InvalidCommentException())
            .Throws(new NullCommentResponseException())

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
                await Assert.ThrowsExceptionAsync<CommentValidationException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            for (int x = 0; x < 2; x++)
            {
                await Assert.ThrowsExceptionAsync<CommentDependencyValidationException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            for (int x = 0; x < 5; x++)
            {
                await Assert.ThrowsExceptionAsync<CommentDependencyException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            await Assert.ThrowsExceptionAsync<CommentServiceException>(async () =>
                await wrapper.TryCatch(delegateMock.Object));
        }
    }
}
