namespace OrcaHello.Web.Api.Tests.Unit.Controllers
{
    public partial class TagsControllerTests
    {
        [TestMethod]
        public async Task Default_GetAllTagsAsync_Expect_TagRemovalResponse()
        {
            TagListResponse response = new()
            {
                Tags = new List<string>() { "Tag1", "Tag2" },
                Count = 2
            };

            _orchestrationServiceMock.Setup(service =>
                service.RetrieveAllTagsAsync())
                .ReturnsAsync(response);

            ActionResult<TagListResponse> actionResult =
                await _controller.GetAllTagsAsync();

            var contentResult = actionResult.Result as ObjectResult;
            Assert.IsNotNull(contentResult);
            Assert.AreEqual(200, contentResult.StatusCode);

            var result = (TagListResponse)contentResult.Value!;

            Assert.IsNotNull(result);
            Assert.AreEqual(2, result.Count);

            _orchestrationServiceMock.Verify(service =>
                service.RetrieveAllTagsAsync(),
                Times.Once);
        }
    }
}
