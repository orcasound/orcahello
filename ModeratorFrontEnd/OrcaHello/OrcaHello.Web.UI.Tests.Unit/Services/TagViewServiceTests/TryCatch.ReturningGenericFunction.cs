using static OrcaHello.Web.UI.Services.TagViewService;

namespace OrcaHello.Web.UI.Tests.Unit.Services
{
    public partial class TagViewServiceTests
    {
        [TestMethod]
        public async Task TryCatch_ReturningGenericFunction_Expect_Exception()
        {
            var wrapper = new TagViewServiceWrapper();
            var delegateMock = new Mock<ReturningGenericFunction<TagItemViewResponse>>();

            delegateMock
                .SetupSequence(p => p())

                .Throws(new NullTagViewRequestException("RequestName"))
                .Throws(new InvalidTagViewException())
                .Throws(new NullTagViewResponseException("ResponseName"))

                .Throws(new TagValidationException())
                .Throws(new DetectionValidationException())
                .Throws(new TagDependencyValidationException())
                .Throws(new DetectionDependencyValidationException())

                .Throws(new TagDependencyException())
                .Throws(new DetectionDependencyException())
                .Throws(new TagServiceException())
                .Throws(new DetectionServiceException())

                .Throws(new Exception());

            for (int x = 0; x < 3; x++)
            {
                await Assert.ThrowsExceptionAsync<TagViewValidationException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            for (int x = 0; x < 4; x++)
            {
                await Assert.ThrowsExceptionAsync<TagViewDependencyValidationException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            for (int x = 0; x < 4; x++)
            {
                await Assert.ThrowsExceptionAsync<TagViewDependencyException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            await Assert.ThrowsExceptionAsync<TagViewServiceException>(async () =>
                await wrapper.TryCatch(delegateMock.Object));
        }
    }
}