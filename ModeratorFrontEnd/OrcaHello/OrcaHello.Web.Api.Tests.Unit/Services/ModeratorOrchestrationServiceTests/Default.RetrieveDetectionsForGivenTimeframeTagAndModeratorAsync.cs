namespace OrcaHello.Web.Api.Tests.Unit.Services
{
    public partial class ModeratorOrchestrationServiceTests
    {
        [TestMethod]
        public async Task Default_RetrieveDetectionsForGivenTimeframeTagAndModeratorAsync_Expect()
        {
            QueryableMetadataForTimeframeTagAndModerator expectedResult = new()
            {
                FromDate = DateTime.Now,
                ToDate = DateTime.Now.AddDays(1),
                QueryableRecords = (new List<Metadata>
                {
                    new Metadata { State = "Positive", Id = Guid.NewGuid().ToString()}
                }).AsQueryable(),
                Moderator = "Moderator",
                Tag = "Tag",
                Page = 1,
                PageSize = 10
            };

            _metadataServiceMock.Setup(service =>
                service.RetrieveMetadataForGivenTimeframeTagAndModeratorAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(expectedResult);

            DetectionListForModeratorAndTagResponse response =
                await _orchestrationService.RetrieveDetectionsForGivenTimeframeTagAndModeratorAsync(DateTime.Now, DateTime.Now.AddDays(1), "Moderator", "Tag", 1, 10);

            Assert.AreEqual(expectedResult.QueryableRecords.Count(), response.Detections.Count());

            _metadataServiceMock.Verify(service =>
                service.RetrieveMetadataForGivenTimeframeTagAndModeratorAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(),
                    It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()),
                    Times.Once);
        }
    }
}