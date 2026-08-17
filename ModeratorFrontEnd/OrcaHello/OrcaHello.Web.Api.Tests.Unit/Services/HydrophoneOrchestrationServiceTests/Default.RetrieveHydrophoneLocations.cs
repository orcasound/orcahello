namespace OrcaHello.Web.Api.Tests.Unit.Services
{
    public partial class HydrophoneOrchestrationServiceTests
    {
        [TestMethod]
        public async Task Default_RetrieveHydrophoneLocations_Expect()
        {
            var expectedResults = new QueryableHydrophoneData
            {
                QueryableRecords = (new List<HydrophoneData> { new() { Attributes = new() { NodeName = "test_id", Name = "test" } } }).AsQueryable(),
                TotalCount = 1
            };

            _hydrophoneServiceMock.Setup(service =>
                service.RetrieveAllHydrophonesAsync())
                .ReturnsAsync(expectedResults);

            HydrophoneListResponse result = await _orchestrationService.
                RetrieveHydrophoneLocations();

            Assert.AreEqual(1, result.Count);

            _hydrophoneServiceMock.Verify(service =>
                service.RetrieveAllHydrophonesAsync(),
                    Times.Once);

        }
    }
}
