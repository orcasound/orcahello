namespace OrcaHello.Web.Api.Controllers
{
    [Route("api/comments")]
    [SwaggerTag("This controller is responsible for retrieving comments submitted by moderators regarding detections.")]
    [ApiController]
    public class CommentsController : ControllerBase
    {
        private readonly ICommentOrchestrationService _commentOrchestrationService;

        public CommentsController(ICommentOrchestrationService commentOrchestrationService)
        {
            _commentOrchestrationService = commentOrchestrationService;
        }

        [HttpGet("negative-unknown")]
        [SwaggerOperation(Summary = "Gets a paginated list of comments for negative and unknown Detections for the given timeframe.")]
        [SwaggerResponse(StatusCodes.Status200OK, "Returns the list of comments for the given timeframe.", typeof(CommentListResponse))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
        [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
        [AllowAnonymous]
        public async ValueTask<ActionResult<CommentListResponse>> GetPaginatedNegativeAndUnknownCommentsForGivenTimeframeAsync(
            [SwaggerParameter("The start date of the search (MM/DD/YYYY).", Required = true)] DateTime? fromDate,
            [SwaggerParameter("The end date of the search (MM/DD/YYYY).", Required = true)] DateTime? toDate,
            [SwaggerParameter("The page in the list to request.", Required = true)] int page,
            [SwaggerParameter("The page size to request.", Required = true)] int pageSize)
        {
            try
            {
                var commentListResponse = await _commentOrchestrationService.RetrieveNegativeAndUnknownCommentsForGivenTimeframeAsync(fromDate, toDate, page, pageSize);

                return Ok(commentListResponse);
            }
            catch (Exception exception)
            {
                if (exception is CommentOrchestrationValidationException ||
                    exception is CommentOrchestrationDependencyValidationException)
                {
                    return BadRequest(ValidatorUtilities.GetInnerMessage(exception));
                }

                if (exception is CommentOrchestrationDependencyException ||
                    exception is CommentOrchestrationServiceException)
                {
                    return Problem(exception.Message);
                }

                return Problem(exception.Message);
            }
        }

        [HttpGet("positive")]
        [SwaggerOperation(Summary = "Gets a paginated list of comments for positive Detections for the given timeframe.")]
        [SwaggerResponse(StatusCodes.Status200OK, "Returns the list of comments for the give timeframe.", typeof(TagListResponse))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
        [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
        [AllowAnonymous]
        public async ValueTask<ActionResult<CommentListResponse>> GetPaginatedPositiveCommentsForGivenTimeframeAsync(
            [SwaggerParameter("The start date of the search (MM/DD/YYYY).", Required = true)] DateTime? fromDate,
            [SwaggerParameter("The end date of the search (MM/DD/YYYY).", Required = true)] DateTime? toDate,
            [SwaggerParameter("The page in the list to request.", Required = true)] int page,
            [SwaggerParameter("The page size to request.", Required = true)] int pageSize)
        {
            try
            {
                var commentListResponse = await _commentOrchestrationService.RetrievePositiveCommentsForGivenTimeframeAsync(fromDate, toDate, page, pageSize);

                return Ok(commentListResponse);
            }
            catch (Exception exception)
            {
                if (exception is CommentOrchestrationValidationException ||
                    exception is CommentOrchestrationDependencyValidationException)
                {
                    return BadRequest(ValidatorUtilities.GetInnerMessage(exception));
                }

                if (exception is CommentOrchestrationDependencyException ||
                    exception is CommentOrchestrationServiceException)
                {
                    return Problem(exception.Message);
                }

                return Problem(exception.Message);
            }
        }

    }
}