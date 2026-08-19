using Moq;
using OrcaHello.Web.Api.Models;
using static OrcaHello.Web.Api.Services.MetricsOrchestrationService;

namespace OrcaHello.Web.Api.Tests.Unit.Services
{
    public partial class MetricsOrchestrationServiceTests
    {
        [TestMethod]
        public async Task TryCatch_MetricsResponse_Expect_Exception()
        {
            var wrapper = new MetricsOrchestrationServiceWrapper();
            var delegateMock = new Mock<ReturningMetricsResponseFunction>();

            delegateMock
               .SetupSequence(p => p())

           .Throws(new InvalidMetricOrchestrationException())

           .Throws(new MetadataValidationException())
           .Throws(new MetadataDependencyValidationException())

           .Throws(new MetadataDependencyException())
           .Throws(new MetadataServiceException())

           .Throws(new Exception());

            await Assert.ThrowsExceptionAsync<MetricOrchestrationValidationException>(async () =>
                await wrapper.TryCatch(delegateMock.Object));

            for (int x = 0; x < 2; x++)
            {
                await Assert.ThrowsExceptionAsync<MetricOrchestrationDependencyValidationException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            for (int x = 0; x < 2; x++)
            {
                await Assert.ThrowsExceptionAsync<MetricOrchestrationDependencyException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            await Assert.ThrowsExceptionAsync<MetricOrchestrationServiceException>(async () =>
                await wrapper.TryCatch(delegateMock.Object));
        }
    }
}
