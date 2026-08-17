using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrcaHello.Web.Api.Tests.Unit.Controllers
{
    public partial class ModeratorsControllerTests
    {
        [TestMethod]
        public async Task Default_GetPaginatedDetectionsForGivenTimeframeTagAndModeratorAsync_Expect_DetectionListForModeratorAndTagResponse()
        {
            DetectionListForModeratorAndTagResponse response = new()
            {
                FromDate = DateTime.Now,
                ToDate = DateTime.Now.AddDays(1),
                Moderator = "Moderator",
                Tag = "Tag",
                Detections = new List<Detection>
                {
                    new() {
                        State = "Positive",
                        Id = Guid.NewGuid().ToString(),
                        Moderator = "Moderator",
                        Tags  = new List<string> { "Tag" }
                    }
                }
            };

            _orchestrationServiceMock.Setup(service =>
                service.RetrieveDetectionsForGivenTimeframeTagAndModeratorAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()))
                .ReturnsAsync(response);

            ActionResult<DetectionListForModeratorAndTagResponse> actionResult =
                await _controller.GetPaginatedDetectionsForGivenTimeframeTagAndModeratorAsync("Moderator", "Tag", DateTime.Now, DateTime.Now.AddDays(1), 1, 10);

            var contentResult = actionResult.Result as ObjectResult;
            Assert.IsNotNull(contentResult);
            Assert.AreEqual(200, contentResult.StatusCode);

            Assert.AreEqual(response.Detections.Count,
                ((DetectionListForModeratorAndTagResponse)contentResult.Value!).Detections.Count);

            _orchestrationServiceMock.Verify(service =>
                service.RetrieveDetectionsForGivenTimeframeTagAndModeratorAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>(), It.IsAny<string>(), It.IsAny<string>(), It.IsAny<int>(), It.IsAny<int>()),
                Times.Once);
        }
    }
}
