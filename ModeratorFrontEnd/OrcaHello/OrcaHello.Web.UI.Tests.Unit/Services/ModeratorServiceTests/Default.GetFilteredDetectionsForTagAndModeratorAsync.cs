namespace OrcaHello.Web.UI.Tests.Unit.Services
{
    public partial class ModeratorServiceTests
    {
        [TestMethod]
        public async Task Default_Expect_GetFilteredDetectionsForTagAndModeratorAsync()
        {
            DetectionListForModeratorAndTagResponse expectedResponse = new()
            {
                Detections = new()
                {
                    new() { Id = Guid.NewGuid().ToString(), Moderator = "John Smith", Tags = new() { "Tag 1" } }
                },
                Moderator = "John Smith",
                Tag = "Tag 1"
            };

            _apiBrokerMock.Setup(broker =>
                broker.GetFilteredDetectionsForTagAndModeratorAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()))
                .ReturnsAsync(expectedResponse);

            DetectionListForModeratorAndTagResponse actualResponse =
                await _service.GetFilteredDetectionsForTagAndModeratorAsync("John Smith", "Tag 1", DateTime.UtcNow.AddDays(-14), DateTime.UtcNow, 1, 10);

            Assert.AreEqual(expectedResponse, actualResponse);

            _apiBrokerMock.Verify(broker =>
                broker.GetFilteredDetectionsForTagAndModeratorAsync(It.IsAny<string>(), It.IsAny<string>(), It.IsAny<string>()),
                Times.Once);
        }
    }
}