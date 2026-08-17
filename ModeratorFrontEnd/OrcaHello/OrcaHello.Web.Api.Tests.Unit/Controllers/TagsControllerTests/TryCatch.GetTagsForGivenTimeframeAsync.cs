namespace OrcaHello.Web.Api.Tests.Unit.Controllers
{
    public partial class TagsControllerTests
    {
        [TestMethod]
        public async Task TryCatch_GetTagsForGivenTimeframeAsync_Expect_Exception()
        {
            _orchestrationServiceMock
                .SetupSequence(p => p.RetrieveTagsForGivenTimePeriodAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()))

                .Throws(new TagOrchestrationValidationException(new Exception()))
                .Throws(new TagOrchestrationDependencyValidationException(new Exception()))

                .Throws(new TagOrchestrationDependencyException(new Exception()))
                .Throws(new TagOrchestrationServiceException(new Exception()))

                .Throws(new Exception());

            await ExecuteRetrieveTags(2, StatusCodes.Status400BadRequest);
            await ExecuteRetrieveTags(3, StatusCodes.Status500InternalServerError);

            _orchestrationServiceMock
                 .Verify(service => service
                    .RetrieveTagsForGivenTimePeriodAsync(It.IsAny<DateTime>(), It.IsAny<DateTime>()),
                    Times.Exactly(5));

        }

        private async Task ExecuteRetrieveTags(int count, int statusCode)
        {
            for (int x = 0; x < count; x++)
            {
                ActionResult<TagListForTimeframeResponse> actionResult =
                    await _controller.GetTagsForGivenTimeframeAsync(DateTime.Now, DateTime.Now.AddDays(1));

                var contentResult = actionResult.Result as ObjectResult;
                Assert.IsNotNull(contentResult);
                Assert.AreEqual(statusCode, contentResult.StatusCode);
            }
        }
    }
}
