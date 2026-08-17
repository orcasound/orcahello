namespace OrcaHello.Web.UI.Tests.Unit.Services
{
    public partial class DetectionServiceTests
    {
        [TestMethod]
        public async Task Default_Expect_RetrieveDetectionAsync()
        {
            var id = Guid.NewGuid().ToString();

            Detection expectedResponse = new()
            {
                Id = id,
                Moderator = "John Smith",
                Tags = new() { "Tag 1" }
            };

            _apiBrokerMock.Setup(broker =>
                broker.GetDetectionAsync(It.IsAny<string>()))
                .ReturnsAsync(expectedResponse);

            Detection actualResponse =
                await _service.RetrieveDetectionAsync(id);

            Assert.AreEqual(expectedResponse, actualResponse);

            _apiBrokerMock.Verify(broker =>
                broker.GetDetectionAsync(It.IsAny<string>()),
                Times.Once);
        }
    }
}