using Moq;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace OrcaHello.Web.UI.Tests.Unit.Services
{
    public partial class DetectionServiceTests
    {
        [TestMethod]
        public async Task Default_Expect_ModerateDetectionsAsync()
        {
            var id = Guid.NewGuid().ToString();

            ModerateDetectionsResponse expectedResponse = new()
            {
                IdsToUpdate = new() { id },
                IdsSuccessfullyUpdated = new() { id }
            };

            _apiBrokerMock.Setup(broker =>
                broker.PutModerateDetectionsAsync(It.IsAny<ModerateDetectionsRequest>()))
                .ReturnsAsync(expectedResponse);

            ModerateDetectionsRequest request = new()
            {
                Ids = new() { id },
                Moderator = "Moderator",
                DateModerated = DateTime.UtcNow,
                State = "Positive"
            };

            ModerateDetectionsResponse actualResponse =
                await _service.ModerateDetectionsAsync(request);

            Assert.AreEqual(expectedResponse, actualResponse);

            _apiBrokerMock.Verify(broker =>
                broker.PutModerateDetectionsAsync(It.IsAny<ModerateDetectionsRequest>()),
                Times.Once);
        }
    }
}