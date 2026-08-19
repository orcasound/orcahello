using static OrcaHello.Web.UI.Services.HydrophoneService;

namespace OrcaHello.Web.UI.Tests.Unit.Services
{
    public partial class HydrophoneServiceTests
    {
        [TestMethod]
        public async Task TryCatch_ReturningGenericFunction_Expect_Exception()
        {
            var wrapper = new HydrophoneServiceWrapper();
            var delegateMock = new Mock<ReturningGenericFunction<List<Hydrophone>>>();

            delegateMock
                .SetupSequence(p => p())

            .Throws(new InvalidHydrophoneException())
            .Throws(new NullHydrophoneResponseException())

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
                await Assert.ThrowsExceptionAsync<HydrophoneValidationException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            for (int x = 0; x < 2; x++)
            {
                await Assert.ThrowsExceptionAsync<HydrophoneDependencyValidationException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            for (int x = 0; x < 5; x++)
            {
                await Assert.ThrowsExceptionAsync<HydrophoneDependencyException>(async () =>
                    await wrapper.TryCatch(delegateMock.Object));
            }

            await Assert.ThrowsExceptionAsync<HydrophoneServiceException>(async () =>
                await wrapper.TryCatch(delegateMock.Object));
        }
    }
}
