using static OrcaHello.Web.UI.Services.TagService;

namespace OrcaHello.Web.UI.Tests.Unit.Services
{
    public partial class TagServiceTests
    {
        [TestMethod]
        public void TryCatch_ReturningGenericFunction_Expect_Exception()
        {
            var wrapper = new TagServiceWrapper();
            var delegateMock = new Mock<ReturningGenericFunction<TagReplaceResponse>>();

            delegateMock
                .SetupSequence(p => p())

            .Throws(new InvalidTagException())
            .Throws(new NullTagRequestException())
            .Throws(new NullTagResponseException())

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
                Assert.ThrowsExceptionAsync<TagValidationException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            for (int x = 0; x < 2; x++)
            {
                Assert.ThrowsExceptionAsync<TagDependencyValidationException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            for (int x = 0; x < 5; x++)
            {
                Assert.ThrowsExceptionAsync<TagDependencyException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            Assert.ThrowsExceptionAsync<TagServiceException>(async () =>
                await wrapper.TryCatch(delegateMock.Object));
        }
    }
}
