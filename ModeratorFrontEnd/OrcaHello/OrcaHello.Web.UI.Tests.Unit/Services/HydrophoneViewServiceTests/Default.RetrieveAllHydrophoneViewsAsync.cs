namespace OrcaHello.Web.UI.Tests.Unit.Services
{
    public partial class HydrophoneViewServiceTests
    {
        [TestMethod]
        public async Task Default_Expect_RetrieveAllHydrophoneViewsAsync()
        {
            List<Hydrophone> expectedResponse = new()
            {
                new() { Name = "Hydrophone #1" }
            };

            _hydrophoneServiceMock.Setup(broker =>
                broker.RetrieveAllHydrophonesAsync())
                .ReturnsAsync(expectedResponse);

            List<HydrophoneItemView> actualResponse =
                await _viewService.RetrieveAllHydrophoneViewsAsync();

            Assert.AreEqual(expectedResponse.Count(), actualResponse.Count());

            _hydrophoneServiceMock.Verify(broker =>
                broker.RetrieveAllHydrophonesAsync(),
                Times.Once);
        }

        [TestMethod]
        public async Task Default_Expect_RetrieveAllHydrophoneViewsAsync_SanitizesIntroHtml()
        {
            List<Hydrophone> expectedResponse = new()
            {
                new()
                {
                    Name = "Hydrophone #1",
                    IntroHtml = "<p>Safe intro</p><script>alert('xss')</script><a href=\"javascript:alert('xss')\">unsafe link</a>"
                }
            };

            _hydrophoneServiceMock.Setup(broker =>
                broker.RetrieveAllHydrophonesAsync())
                .ReturnsAsync(expectedResponse);

            List<HydrophoneItemView> actualResponse =
                await _viewService.RetrieveAllHydrophoneViewsAsync();

            Assert.AreEqual(expectedResponse.Count(), actualResponse.Count());
            StringAssert.Contains(actualResponse[0].IntroHtml, "<p>Safe intro</p>");
            Assert.IsFalse(actualResponse[0].IntroHtml.Contains("<script"));
            Assert.IsFalse(actualResponse[0].IntroHtml.Contains("javascript:"));

            _hydrophoneServiceMock.Verify(broker =>
                broker.RetrieveAllHydrophonesAsync(),
                Times.Once);
        }
    }
}
