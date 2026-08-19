namespace OrcaHello.Web.Api.Controllers
{
    [Route("api/moderators")]
    [SwaggerTag("This controller is responsible for retrieving various kinds of data that are relevant to the moderator role.")]
    [ApiController]
    public class ModeratorsController : ControllerBase
    {
        private readonly IModeratorOrchestrationService _moderatorOrchestrationService;

        public ModeratorsController(IModeratorOrchestrationService moderatorOrchestrationService)
        {
            _moderatorOrchestrationService = moderatorOrchestrationService;
        }

        [HttpGet]
        [SwaggerOperation(Summary = "Gets a list of unique moderators.")]
        [SwaggerResponse(StatusCodes.Status200OK, "Returns the list of moderators.", typeof(ModeratorListResponse))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
        [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
        [AllowAnonymous]
        public async ValueTask<ActionResult<ModeratorListResponse>> GetModeratorsAsync()
        {
            try
            {
                var moderatorListResponse = await _moderatorOrchestrationService
                .RetrieveModeratorsAsync();

                return Ok(moderatorListResponse);
            }
            catch (Exception exception)
            {
                if (exception is ModeratorOrchestrationValidationException ||
                    exception is ModeratorOrchestrationDependencyValidationException)
                {
                    return BadRequest(ValidatorUtilities.GetInnerMessage(exception));
                }

                if (exception is ModeratorOrchestrationDependencyException ||
                    exception is ModeratorOrchestrationServiceException)
                {
                    return Problem(exception.Message);
                }

                return Problem(exception.Message);
            }
        }

        [HttpGet("{moderator}/comments/positive")]
        [SwaggerOperation(Summary = "Gets a paginated list of comments for positive Detections for the given timeframe and moderator.")]
        [SwaggerResponse(StatusCodes.Status200OK, "Returns the list of comments for the give timeframe and moderator.", typeof(CommentListForModeratorResponse))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
        [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
        [AllowAnonymous]
        public async ValueTask<ActionResult<CommentListForModeratorResponse>> GetPaginatedPositiveCommentsForGivenTimeframeAndModeratorAsync(
            [SwaggerParameter("The name of the moderator.", Required = true)] string moderator,
            [SwaggerParameter("The start date of the search (MM/DD/YYYY).", Required = true)] DateTime? fromDate,
            [SwaggerParameter("The end date of the search (MM/DD/YYYY).", Required = true)] DateTime? toDate,
            [SwaggerParameter("The page in the list to request.", Required = true)] int page,
            [SwaggerParameter("The page size to request.", Required = true)] int pageSize)
        {
            try
            {
                var commentListForModeratorResponse = await _moderatorOrchestrationService
                    .RetrievePositiveCommentsForGivenTimeframeAndModeratorAsync(fromDate, toDate, moderator, page, pageSize);

                return Ok(commentListForModeratorResponse);
            }
            catch (Exception exception)
            {
                if (exception is ModeratorOrchestrationValidationException ||
                    exception is ModeratorOrchestrationDependencyValidationException)
                {
                    return BadRequest(ValidatorUtilities.GetInnerMessage(exception));
                }

                if (exception is ModeratorOrchestrationDependencyException ||
                    exception is ModeratorOrchestrationServiceException)
                {
                    return Problem(exception.Message);
                }

                return Problem(exception.Message);
            }
        }

        [HttpGet("{moderator}/comments/negative-unknown")]
        [SwaggerOperation(Summary = "Gets a paginated list of comments for negative or unknown Detections for the given timeframe and moderator.")]
        [SwaggerResponse(StatusCodes.Status200OK, "Returns the list of comments for the give timeframe and moderator.", typeof(CommentListForModeratorResponse))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
        [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
        [AllowAnonymous]
        public async ValueTask<ActionResult<CommentListForModeratorResponse>> GetPaginatedNegativeAndUnknownCommentsForGivenTimeframeAndModeratorAsync(
        [SwaggerParameter("The name of the moderator.", Required = true)] string moderator,
        [SwaggerParameter("The start date of the search (MM/DD/YYYY).", Required = true)] DateTime? fromDate,
        [SwaggerParameter("The end date of the search (MM/DD/YYYY).", Required = true)] DateTime? toDate,
        [SwaggerParameter("The page in the list to request.", Required = true)] int page,
        [SwaggerParameter("The page size to request.", Required = true)] int pageSize)
        {
            try
            {
                var commentListForModeratorResponse = await _moderatorOrchestrationService
                    .RetrieveNegativeAndUnknownCommentsForGivenTimeframeAndModeratorAsync(fromDate, toDate, moderator, page, pageSize);

                return Ok(commentListForModeratorResponse);
            }
            catch (Exception exception)
            {
                if (exception is ModeratorOrchestrationValidationException ||
                    exception is ModeratorOrchestrationDependencyValidationException)
                {
                    return BadRequest(ValidatorUtilities.GetInnerMessage(exception));
                }

                if (exception is ModeratorOrchestrationDependencyException ||
                    exception is ModeratorOrchestrationServiceException)
                {
                    return Problem(exception.Message);
                }

                return Problem(exception.Message);
            }
        }

        [HttpGet("{moderator}/detections/{tag}")]
        [SwaggerOperation(Summary = "Gets a paginated list of detections for the given timeframe, tag and moderator.")]
        [SwaggerResponse(StatusCodes.Status200OK, "Returns the list of detections for the give timeframe, tag and moderator.", typeof(DetectionListForModeratorAndTagResponse))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
        [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
        [Authorize("Moderators")]
        public async ValueTask<ActionResult<DetectionListForModeratorAndTagResponse>> GetPaginatedDetectionsForGivenTimeframeTagAndModeratorAsync(
             [SwaggerParameter("The name of the moderator.", Required = true)] string moderator,
             [SwaggerParameter("The tag.", Required = true)] string tag,
             [SwaggerParameter("The start date of the search (MM/DD/YYYY).", Required = true)] DateTime? fromDate,
             [SwaggerParameter("The end date of the search (MM/DD/YYYY).", Required = true)] DateTime? toDate,
             [SwaggerParameter("The page in the list to request.", Required = true)] int page,
             [SwaggerParameter("The page size to request.", Required = true)] int pageSize)
        {
            try
            {
                var detectionListForModeratorAndTagResponse = await _moderatorOrchestrationService
                    .RetrieveDetectionsForGivenTimeframeTagAndModeratorAsync(fromDate, toDate, moderator, tag, page, pageSize);

                return Ok(detectionListForModeratorAndTagResponse);
            }
            catch (Exception exception)
            {
                if (exception is ModeratorOrchestrationValidationException ||
                    exception is ModeratorOrchestrationDependencyValidationException)
                {
                    return BadRequest(ValidatorUtilities.GetInnerMessage(exception));
                }

                if (exception is ModeratorOrchestrationDependencyException ||
                    exception is ModeratorOrchestrationServiceException)
                {
                    return Problem(exception.Message);
                }

                return Problem(exception.Message);
            }
        }

        [HttpGet("{moderator}/tags")]
        [SwaggerOperation(Summary = "Gets a list of tags for the given timeframe and moderator.")]
        [SwaggerResponse(StatusCodes.Status200OK, "Returns the list of tags for the give timeframe and moderator.", typeof(CommentListForModeratorResponse))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
        [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
        [AllowAnonymous]
        public async ValueTask<ActionResult<TagListForModeratorResponse>> GetTagsForGivenTimeframeAndModeratorAsync(
            [SwaggerParameter("The name of the moderator.", Required = true)] string moderator,
            [SwaggerParameter("The start date of the search (MM/DD/YYYY).", Required = true)] DateTime? fromDate,
            [SwaggerParameter("The end date of the search (MM/DD/YYYY).", Required = true)] DateTime? toDate)
        {
            try
            {
                var tagListForModeratorResponse = await _moderatorOrchestrationService
                    .RetrieveTagsForGivenTimePeriodAndModeratorAsync(fromDate, toDate, moderator);

                return Ok(tagListForModeratorResponse);
            }
            catch (Exception exception)
            {
                if (exception is ModeratorOrchestrationValidationException ||
                    exception is ModeratorOrchestrationDependencyValidationException)
                {
                    return BadRequest(ValidatorUtilities.GetInnerMessage(exception));
                }

                if (exception is ModeratorOrchestrationDependencyException ||
                    exception is ModeratorOrchestrationServiceException)
                {
                    return Problem(exception.Message);
                }

                return Problem(exception.Message);
            }
        }

        [HttpGet("{moderator}/metrics")]
        [SwaggerOperation(Summary = "Gets a list of review metrics for the given timeframe and moderator.")]
        [SwaggerResponse(StatusCodes.Status200OK, "Returns the list of metrics for the give timeframe and moderator.", typeof(MetricsForModeratorResponse))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
        [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
        [AllowAnonymous]
        public async ValueTask<ActionResult<MetricsForModeratorResponse>> GetMetricsForGivenTimeframeAndModeratorAsync(
            [SwaggerParameter("The name of the moderator.", Required = true)] string moderator,
            [SwaggerParameter("The start date of the search (MM/DD/YYYY).", Required = true)] DateTime? fromDate,
            [SwaggerParameter("The end date of the search (MM/DD/YYYY).", Required = true)] DateTime? toDate)
        {
            try
            {
                var metricsForModeratorResponse = await _moderatorOrchestrationService
                    .RetrieveMetricsForGivenTimeframeAndModeratorAsync(fromDate, toDate, moderator);

                return Ok(metricsForModeratorResponse);
            }
            catch (Exception exception)
            {
                if (exception is ModeratorOrchestrationValidationException ||
                    exception is ModeratorOrchestrationDependencyValidationException)
                {
                    return BadRequest(ValidatorUtilities.GetInnerMessage(exception));
                }

                if (exception is ModeratorOrchestrationDependencyException ||
                    exception is ModeratorOrchestrationServiceException)
                {
                    return Problem(exception.Message);
                }

                return Problem(exception.Message);
            }
        }
    }
}