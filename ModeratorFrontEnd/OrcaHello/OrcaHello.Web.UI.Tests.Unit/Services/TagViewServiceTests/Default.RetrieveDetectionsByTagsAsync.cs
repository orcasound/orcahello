namespace OrcaHello.Web.UI.Tests.Unit.Services
{
    public partial class TagViewServiceTests
    {
        [TestMethod]
        public async Task Default_Expect_RetrieveDetectionsByTagsAsync_And()
        {
            DetectionListForTagResponse expectedResponse = new()
            {
                Detections = new()
                {
                    new() {
                        Id = Guid.NewGuid().ToString(),
                        Comments = "These are the comments.",
                        Location = new()
                        {
                            Name = "Location 1",
                            Longitude = 1.00,
                            Latitude = 1.00
                        }
                    }
                },
                TotalCount = 1
            };

            _detectionServiceMock.Setup(broker =>
                broker.RetrieveFilteredAndPaginatedDetectionsForTagAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(expectedResponse);

            PaginatedDetectionsByTagsAndDateRequest request = new()
            {
                Tags = new() { "Tag 1", "Tag 2" },
                Logic = LogicalOperator.And,
                Page = 1,
                PageSize = 10,
                FromDate = DateTime.UtcNow.AddDays(-14),
                ToDate = DateTime.UtcNow
            };

            DetectionItemViewResponse actualResponse = await _viewService.RetrieveDetectionsByTagsAsync(request);

            Assert.AreEqual(expectedResponse.Detections.Count(), actualResponse.DetectionItemViews.Count());

            _detectionServiceMock.Verify(broker =>
                broker.RetrieveFilteredAndPaginatedDetectionsForTagAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>()),
                Times.Once);
        }

        [TestMethod]
        public async Task Default_Expect_RetrieveDetectionsByTagsAsync_Or()
        {
            DetectionListForTagResponse expectedResponse = new()
            {
                Detections = new()
                {
                    new() {
                        Id = Guid.NewGuid().ToString(),
                        Comments = "These are the comments.",
                        Location = new()
                        {
                            Name = "Location 1",
                            Longitude = 1.00,
                            Latitude = 1.00
                        }
                    }
                },
                TotalCount = 1
            };

            _detectionServiceMock.Setup(broker =>
                broker.RetrieveFilteredAndPaginatedDetectionsForTagAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(expectedResponse);

            PaginatedDetectionsByTagsAndDateRequest request = new()
            {
                Tags = new() { "Tag 1", "Tag 2" },
                Logic = LogicalOperator.Or,
                Page = 1,
                PageSize = 10,
                FromDate = DateTime.UtcNow.AddDays(-14),
                ToDate = DateTime.UtcNow
            };

            DetectionItemViewResponse actualResponse = await _viewService.RetrieveDetectionsByTagsAsync(request);

            Assert.AreEqual(expectedResponse.Detections.Count(), actualResponse.DetectionItemViews.Count());

            _detectionServiceMock.Verify(broker =>
                broker.RetrieveFilteredAndPaginatedDetectionsForTagAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<int>(), It.IsAny<int>()),
                Times.Once);
        }
    }
}