namespace OrcaHello.Web.Api.Tests.Unit.Controllers
{
    public partial class DetectionsControllerTests
    {

        [TestMethod]
        public async Task Default_GetDetectionByIdAsync_Expect_Detection()
        {
            Detection expectedResult = new();

            _orchestrationServiceMock.Setup(service =>
                service.RetrieveDetectionByIdAsync(It.IsAny<string>()))
            .ReturnsAsync(expectedResult);

            ActionResult<Detection> actionResult =
                await _controller.GetDetectionByIdAsync(Guid.NewGuid().ToString());


            var contentResult = actionResult.Result as ObjectResult;
            Assert.IsNotNull(contentResult);
            Assert.AreEqual(200, contentResult.StatusCode);

            Assert.IsNotNull((Detection)contentResult.Value!);

            _orchestrationServiceMock.Verify(service =>
               service.RetrieveDetectionByIdAsync(It.IsAny<string>()),
                Times.Once);
        }
    }
}
