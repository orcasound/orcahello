namespace OrcaHello.Web.Api.Controllers
{
    [Route("api/tags")]
    [SwaggerTag("This controller is responsible for retrieving and curating tags submitted by moderators against reviewed detections.")]
    [ApiController]
    public class TagsController : ControllerBase
    {
        private readonly ITagOrchestrationService _tagOrchestrationService;

        public TagsController(ITagOrchestrationService tagOrchestrationService)
        {
            _tagOrchestrationService = tagOrchestrationService;
        }

        [HttpGet]
        [SwaggerOperation(Summary = "Gets a list of all unique tags.")]
        [SwaggerResponse(StatusCodes.Status200OK, "Returns the list of tags.", typeof(TagListResponse))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
        [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
        [AllowAnonymous]
        public async ValueTask<ActionResult<TagListResponse>> GetAllTagsAsync()
        {
            try
            {
                var tagListResponse = await _tagOrchestrationService.RetrieveAllTagsAsync();

                return Ok(tagListResponse);
            }
            catch (Exception exception)
            {
                if (exception is TagOrchestrationValidationException ||
                    exception is TagOrchestrationDependencyValidationException)
                    return BadRequest(ValidatorUtilities.GetInnerMessage(exception));

                if (exception is TagOrchestrationDependencyException ||
                    exception is TagOrchestrationServiceException)
                    return Problem(exception.Message);

                return Problem(exception.Message);
            }
        }

        [HttpGet("bytimeframe")]
        [SwaggerOperation(Summary = "Gets a list of tags for the given timeframe.")]
        [SwaggerResponse(StatusCodes.Status200OK, "Returns the list of tags for the given timeframe.", typeof(TagListForTimeframeResponse))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
        [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
        [AllowAnonymous]
        public async ValueTask<ActionResult<TagListForTimeframeResponse>> GetTagsForGivenTimeframeAsync(
            [SwaggerParameter("The start date of the search (MM/DD/YYYY).", Required = true)] DateTime? fromDate,
            [SwaggerParameter("The end date of the search (MM/DD/YYYY).", Required = true)] DateTime? toDate)
        {
            try
            {
                var tagListForTimeframeResponse = await _tagOrchestrationService.RetrieveTagsForGivenTimePeriodAsync(fromDate, toDate);

                return Ok(tagListForTimeframeResponse);
            }
            catch (Exception exception)
            {
                if (exception is TagOrchestrationValidationException ||
                    exception is TagOrchestrationDependencyValidationException)
                    return BadRequest(ValidatorUtilities.GetInnerMessage(exception));

                if (exception is TagOrchestrationDependencyException ||
                    exception is TagOrchestrationServiceException)
                    return Problem(exception.Message);

                return Problem(exception.Message);
            }
        }

        [HttpDelete("{tag}")]
        [SwaggerOperation(Summary = "Delete all occurences of the tag from all detections.")]
        [SwaggerResponse(StatusCodes.Status200OK, "The tag was deleted from all detections.")]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
        [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
        [Authorize("Moderators")]
        public async ValueTask<ActionResult<TagRemovalResponse>> DeleteTagFromAllDetectionsAsync(
            [SwaggerParameter("The tag to remove.", Required = true)] string tag)
        {
            try
            {
                var result = await _tagOrchestrationService.RemoveTagFromAllDetectionsAsync(tag);

                return Ok(result);
            }
            catch (Exception exception)
            {
                if (exception is TagOrchestrationValidationException ||
                    exception is TagOrchestrationDependencyValidationException)
                    return BadRequest(ValidatorUtilities.GetInnerMessage(exception));

                if (exception is TagOrchestrationDependencyException ||
                    exception is TagOrchestrationServiceException)
                    return Problem(exception.Message);

                return Problem(exception.Message);
            }
        }

        [HttpPut("replace")]
        [SwaggerOperation(Summary = "Replace the occurence of a tag in all detections with another tag.")]
        [SwaggerResponse(StatusCodes.Status200OK, "The tag was replaced in all detections.")]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
        [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
        [Authorize("Moderators")]
        public async ValueTask<ActionResult<TagReplaceResponse>> ReplaceTagInAllDetectionsAsync(
            [FromBody][SwaggerParameter("The old and new tag.", Required = true)] ReplaceTagRequest request)
        {
            try
            {
                var result = await _tagOrchestrationService.ReplaceTagInAllDetectionsAsync(request);

                return Ok(result);
            }
            catch (Exception exception)
            {
                if (exception is TagOrchestrationValidationException ||
                    exception is TagOrchestrationDependencyValidationException)
                    return BadRequest(ValidatorUtilities.GetInnerMessage(exception));

                if (exception is TagOrchestrationDependencyException ||
                    exception is TagOrchestrationServiceException)
                    return Problem(exception.Message);

                return Problem(exception.Message);
            }
        }
    }
}