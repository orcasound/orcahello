using static OrcaHello.Web.UI.Services.DetectionViewService;

namespace OrcaHello.Web.UI.Tests.Unit.Services
{
    public partial class DetectionViewServiceTests
    {
        [TestMethod]
        public async Task TryCatch_ReturningGenericFunction_Expect_Exception()
        {
            var wrapper = new DetectionViewServiceWrapper();
            var delegateMock = new Mock<ReturningGenericFunction<DetectionItemViewResponse>>();

            delegateMock
                .SetupSequence(p => p())

                .Throws(new NullDetectionViewRequestException("RequestName"))
                .Throws(new InvalidDetectionViewException())
                .Throws(new NullDetectionViewResponseException("ResponseName"))

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
                await Assert.ThrowsExceptionAsync<DetectionViewValidationException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            for (int x = 0; x < 4; x++)
            {
                await Assert.ThrowsExceptionAsync<DetectionViewDependencyValidationException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            for (int x = 0; x < 4; x++)
            {
                await Assert.ThrowsExceptionAsync<DetectionViewDependencyException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            await Assert.ThrowsExceptionAsync<DetectionViewServiceException>(async () =>
                await wrapper.TryCatch(delegateMock.Object));
        }
    }
}
