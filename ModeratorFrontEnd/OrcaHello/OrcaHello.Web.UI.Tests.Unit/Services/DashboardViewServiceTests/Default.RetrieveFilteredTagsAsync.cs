namespace OrcaHello.Web.UI.Tests.Unit.Services
{
    public partial class DashboardViewServiceTests
    {
        [TestMethod]
        public async Task Default_Expect_RetrieveFilteredTagsAsync()
        {
            TagListForTimeframeResponse expectedResponse = new()
            {
                Tags = new() { "Tag 1", "Tag 2" },
                FromDate = DateTime.UtcNow.AddDays(-14),
                ToDate = DateTime.UtcNow,
                Count = 2
            };

            _tagServiceMock.Setup(service =>
                service.RetrieveFilteredTagsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))
                    .ReturnsAsync(expectedResponse);

            TagsByDateRequest request = new()
            {
                FromDate = DateTime.UtcNow.AddDays(-14),
                ToDate = DateTime.UtcNow,
            };

            List<string> actualResponse =
                await _viewService.RetrieveFilteredTagsAsync(request);

            Assert.AreEqual(expectedResponse.Count, actualResponse.Count);

            _tagServiceMock.Verify(service =>
                service.RetrieveFilteredTagsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()),
                Times.Once);
        }
    }
}