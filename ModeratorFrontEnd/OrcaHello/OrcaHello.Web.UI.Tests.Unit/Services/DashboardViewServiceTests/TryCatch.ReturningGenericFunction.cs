using System;
using static OrcaHello.Web.UI.Services.DashboardViewService;

namespace OrcaHello.Web.UI.Tests.Unit.Services
{
    public partial class DashboardViewServiceTests
    {
        [TestMethod]
        public async Task TryCatch_ReturningGenericFunction_Expect_Exception()
        {
            var wrapper = new DashboardViewServiceWrapper();
            var delegateMock = new Mock<ReturningGenericFunction<DetectionItemViewResponse>>();

            delegateMock
                .SetupSequence(p => p())

                .Throws(new NullDashboardViewRequestException("RequestName"))
                .Throws(new InvalidDashboardViewException())
                .Throws(new NullDashboardViewResponseException("ResponseName"))

                .Throws(new TagValidationException())
                .Throws(new DetectionValidationException())
                .Throws(new MetricsValidationException())
                .Throws(new CommentValidationException())
                .Throws(new ModeratorValidationException())
                .Throws(new TagDependencyValidationException())
                .Throws(new DetectionDependencyValidationException())
                .Throws(new MetricsDependencyValidationException())
                .Throws(new CommentDependencyValidationException())
                .Throws(new ModeratorDependencyValidationException())

                .Throws(new TagDependencyException())
                .Throws(new DetectionDependencyException())
                .Throws(new MetricsDependencyException())
                .Throws(new CommentDependencyException())
                .Throws(new ModeratorDependencyException())
                .Throws(new TagServiceException())
                .Throws(new DetectionServiceException())
                .Throws(new MetricsServiceException())
                .Throws(new CommentServiceException())
                .Throws(new ModeratorServiceException())

                .Throws(new Exception());

            for (int x = 0; x < 3; x++)
            {
                await Assert.ThrowsExceptionAsync<DashboardViewValidationException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            for (int x = 0; x < 10; x++)
            {
                await Assert.ThrowsExceptionAsync<DashboardViewDependencyValidationException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            for (int x = 0; x < 10; x++)
            {
                await Assert.ThrowsExceptionAsync<DashboardViewDependencyException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            await Assert.ThrowsExceptionAsync<DashboardViewServiceException>(async () =>
                await wrapper.TryCatch(delegateMock.Object));
        }
    }
}