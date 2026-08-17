namespace OrcaHello.Web.Api.Tests.Unit.Controllers
{
    public partial class DetectionsControllerTests
    {
        [TestMethod]
        public async Task Default_GetPaginatedDetectionsAsync_Expect_DetectionListResponse()
        {
            DetectionListResponse response = new()
            {
                Count = 2,
                FromDate = DateTime.Now,
                ToDate = DateTime.Now.AddDays(1),
                Detections = new List<Detection> { new() },
                State = "Positive",
                SortBy = "timestamp",
                SortOrder = "desc",
                Location = "location"
            };

            _orchestrationServiceMock.Setup(service =>
                service.RetrieveFilteredDetectionsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(response);

            ActionResult<DetectionListResponse> actionResult =
                await _controller.GetPaginatedDetectionsAsync("Positive", DateTime.Now, DateTime.Now.AddDays(1), "timestamp", true, 1, 10, null!);

            var contentResult = actionResult.Result as ObjectResult;
            Assert.IsNotNull(contentResult);
            Assert.AreEqual(200, contentResult.StatusCode);

            Assert.AreEqual(response.Detections.Count,
                ((DetectionListResponse)contentResult.Value!).Detections.Count);

            _orchestrationServiceMock.Verify(service =>
               service.RetrieveFilteredDetectionsAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(),
                It.IsAny<string>(), It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()),
                Times.Once);
        }
    }
}
