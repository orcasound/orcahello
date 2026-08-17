namespace OrcaHello.Web.UI.Tests.Unit.Services
{
    public partial class DetectionViewServiceTests
    {
        [TestMethod]
        public async Task Default_Expect_ModerateDetectionsAsync()
        {
            var id = Guid.NewGuid().ToString();

            ModerateDetectionsResponse expectedResponse = new()
            {
                IdsToUpdate = new() { id },
                IdsSuccessfullyUpdated = new List<string> { id }
            };

            _detectionServiceMock.Setup(service =>
                service.ModerateDetectionsAsync(It.IsAny<ModerateDetectionsRequest>()))
                .ReturnsAsync(expectedResponse);

            ModerateDetectionsResponse actualResponse =
                await _viewService.ModerateDetectionsAsync(new() { id }, "Positive", "Moderator", "comments", "Tag 1,Tag 2");

            Assert.AreEqual(expectedResponse.IdsSuccessfullyUpdated, actualResponse.IdsSuccessfullyUpdated);

            _detectionServiceMock.Verify(service =>
                service.ModerateDetectionsAsync(It.IsAny<ModerateDetectionsRequest>()),
                Times.Once);
        }

        [TestMethod]
        public async Task Default_Expect_ModerateDetectionsAsync_EmptyCommentsAndTags()
        {
            var id = Guid.NewGuid().ToString();

            ModerateDetectionsResponse expectedResponse = new()
            {
                IdsToUpdate = new() { id },
                IdsSuccessfullyUpdated = new List<string> { id }
            };

            _detectionServiceMock.Setup(service =>
                service.ModerateDetectionsAsync(It.IsAny<ModerateDetectionsRequest>()))
                .ReturnsAsync(expectedResponse);

            ModerateDetectionsResponse actualResponse =
                await _viewService.ModerateDetectionsAsync(new() { id }, "Positive", "Moderator", string.Empty, string.Empty);

            Assert.AreEqual(expectedResponse.IdsSuccessfullyUpdated, actualResponse.IdsSuccessfullyUpdated);

            _detectionServiceMock.Verify(service =>
                service.ModerateDetectionsAsync(It.IsAny<ModerateDetectionsRequest>()),
                Times.Once);
        }
    }
}