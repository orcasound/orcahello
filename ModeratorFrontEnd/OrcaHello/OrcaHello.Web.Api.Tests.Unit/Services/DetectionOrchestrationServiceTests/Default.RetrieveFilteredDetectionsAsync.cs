using Moq;
using OrcaHello.Web.Api.Models;
using OrcaHello.Web.Shared.Models.Detections;

namespace OrcaHello.Web.Api.Tests.Unit.Services
{
    public partial class DetectionOrchestrationServiceTests
    {
        [TestMethod]
        public async Task Default_RetrieveFilteredDetectionsAsync_Expect()
        {
            var nullModeratedMetadata = CreateRandomMetadata();
            nullModeratedMetadata.DateModerated = null!;

            var expectedResults = new QueryableMetadataFiltered
            {
                QueryableRecords = (new List<Metadata> { CreateRandomMetadata(), nullModeratedMetadata }).AsQueryable(),
                FromDate = DateTime.Now,
                ToDate = DateTime.Now.AddDays(1),
                TotalCount = 1,
                Page = 1,
                PageSize = 10,
                State = "Positive",
                Location = "Haro Straight",
                SortBy = "timestamp",
                SortOrder = "DESC"
            };

            _metadataServiceMock.Setup(service =>
                service.RetrievePaginatedMetadataAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(),
                    It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(expectedResults);

            DetectionListResponse result = await _orchestrationService.
                RetrieveFilteredDetectionsAsync(DateTime.Now, DateTime.Now.AddDays(1), "Positive", "timestamp", true, null!, 1, 10);

            Assert.AreEqual(expectedResults.QueryableRecords.Count(), result.Detections.Count);

            _metadataServiceMock.Verify(service =>
                service.RetrievePaginatedMetadataAsync(It.IsAny<string>(), It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(),
                    It.IsAny<bool>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()),
                    Times.Once);
        }
    }
}
