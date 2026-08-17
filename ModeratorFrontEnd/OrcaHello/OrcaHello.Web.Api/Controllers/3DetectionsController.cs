namespace OrcaHello.Web.Api.Controllers
{
    [Route("api/detections")]
    [SwaggerTag("This controller is responsible for retrieving and curating detections.")]
    [ApiController]
    public class DetectionsController : ControllerBase
    {
        private readonly IDetectionOrchestrationService _detectionOrchestrationService;

        public DetectionsController(IDetectionOrchestrationService detectionOrchestrationService)
        {
            _detectionOrchestrationService = detectionOrchestrationService;
        }

        [HttpGet("{detectionId}")]
        [SwaggerOperation(Summary = "Gets a Detection for the given id.")]
        [SwaggerResponse(StatusCodes.Status200OK, "Returns the detection for the given id.", typeof(Detection))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
        [SwaggerResponse(StatusCodes.Status404NotFound, "If the detection for the given id was not found.")]
        [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
        [AllowAnonymous]
        public async ValueTask<ActionResult<Detection>> GetDetectionByIdAsync(
        [SwaggerParameter("The desired id.", Required = true)] string detectionId)
        {
            try
            {
                var detection = await _detectionOrchestrationService.RetrieveDetectionByIdAsync(detectionId);

                return Ok(detection);
            }
            catch (Exception exception)
            {
                if (exception is DetectionOrchestrationValidationException &&
                    exception.InnerException is NotFoundMetadataException)
                    return NotFound(ValidatorUtilities.GetInnerMessage(exception));

                if (exception is DetectionOrchestrationValidationException ||
                    exception is DetectionOrchestrationDependencyValidationException)
                    return BadRequest(ValidatorUtilities.GetInnerMessage(exception));

                if (exception is DetectionOrchestrationDependencyException ||
                    exception is DetectionOrchestrationServiceException)
                    return Problem(exception.Message);

                return Problem(exception.Message);
            }
        }

        [HttpGet("bytag/{tag}")]
        [SwaggerOperation(Summary = "Gets a list of Detections for the given timeframe and tag.")]
        [SwaggerResponse(StatusCodes.Status200OK, "Returns the list of detections for the given timeframe and tag.", typeof(DetectionListForTagResponse))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
        [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
        [AllowAnonymous]
        public async ValueTask<ActionResult<DetectionListForTagResponse>> GetPaginatedDetectionsForGivenTimeframeAndTagAsync(
            [SwaggerParameter("The desired tag(s) (i.e. tag1,tag2 for AND tag1|tag2 for OR).", Required = true)] string tag,
            [SwaggerParameter("The start date of the search (MM/DD/YYYY).", Required = true)] DateTime? fromDate,
            [SwaggerParameter("The end date of the search (MM/DD/YYYY).", Required = true)] DateTime? toDate,
            [SwaggerParameter("The page in the list to request.", Required = true)] int page,
            [SwaggerParameter("The page size to request.", Required = true)] int pageSize)
        {
            try
            {
                var detectionListForTagResponse = await _detectionOrchestrationService.RetrieveDetectionsForGivenTimeframeAndTagAsync(fromDate, toDate, tag, page, pageSize);

                return Ok(detectionListForTagResponse);
            }
            catch (Exception exception)
            {
                if (exception is DetectionOrchestrationValidationException ||
                    exception is DetectionOrchestrationDependencyValidationException)
                    return BadRequest(ValidatorUtilities.GetInnerMessage(exception));

                if (exception is DetectionOrchestrationDependencyException ||
                    exception is DetectionOrchestrationServiceException)
                    return Problem(exception.Message);

                return Problem(exception.Message);
            }
        }


        [HttpGet("byinterestlabel/{interestLabel}")]
        [SwaggerOperation(Summary = "Gets a list of Detections for the given interest label.")]
        [SwaggerResponse(StatusCodes.Status200OK, "Returns the list of detections for the given interest label.", typeof(DetectionListForInterestLabelResponse))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
        [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
        [AllowAnonymous]
        public async ValueTask<ActionResult<DetectionListForInterestLabelResponse>> GetDetectionsForGivenInterestLabelAsync(
            [SwaggerParameter("The desired interest label.", Required = true)] string interestLabel)
        {
            try
            {
                var detectionListForInterestLabelResponse = await _detectionOrchestrationService.
                    RetrieveDetectionsForGivenInterestLabelAsync(interestLabel);

                return Ok(detectionListForInterestLabelResponse);
            }
            catch (Exception exception)
            {
                if (exception is DetectionOrchestrationValidationException ||
                    exception is DetectionOrchestrationDependencyValidationException)
                    return BadRequest(ValidatorUtilities.GetInnerMessage(exception));

                if (exception is DetectionOrchestrationDependencyException ||
                    exception is DetectionOrchestrationServiceException)
                    return Problem(exception.Message);

                return Problem(exception.Message);
            }
        }

        [HttpGet]
        [SwaggerOperation(Summary = "Gets a list of Detections for the passed filter conditions.")]
        [SwaggerResponse(StatusCodes.Status200OK, "Returns the list of detections for the given filter conditions.", typeof(DetectionListResponse))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
        [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
        [AllowAnonymous]
        public async ValueTask<ActionResult<DetectionListResponse>> GetPaginatedDetectionsAsync(
 [SwaggerParameter("The desired state: Unreviewed, Negative, Positive, or Unknown.", Required = true)] string state,
            [SwaggerParameter("The start date of the search (MM/DD/YYYY).", Required = true)] DateTime? fromDate,
            [SwaggerParameter("The end date of the search (MM/DD/YYYY).", Required = true)] DateTime? toDate,
            [SwaggerParameter("The name of the field to sort by: date, confidence, moderator, moderateddate.", Required = true)] string sortBy,
            [SwaggerParameter("Flag indicating if the sort order should be descending.", Required = true)] bool isDescending,
            [SwaggerParameter("The page in the list to request.", Required = true)] int page,
            [SwaggerParameter("The page size to request.", Required = true)] int pageSize,
            [SwaggerParameter("The name of the location of the Hydrophone (optional).", Required = false)] string location = "")
        {
            try
            {
                var detectionListResponse = await _detectionOrchestrationService
                    .RetrieveFilteredDetectionsAsync(fromDate, toDate, state, sortBy, isDescending, location, page, pageSize);

                return Ok(detectionListResponse);
            }
            catch (Exception exception)
            {
                if (exception is DetectionOrchestrationValidationException ||
                    exception is DetectionOrchestrationDependencyValidationException)
                    return BadRequest(ValidatorUtilities.GetInnerMessage(exception));

                if (exception is DetectionOrchestrationDependencyException ||
                    exception is DetectionOrchestrationServiceException)
                    return Problem(exception.Message);

                return Problem(exception.Message);
            }
        }

        [HttpPut("moderate")]
        [SwaggerOperation(Summary = "Perform Moderator-related update of one or more existing Detections.")]
        [SwaggerResponse(StatusCodes.Status200OK, "Returns the detection results for the given ids.", typeof(ModerateDetectionsResponse))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
        [SwaggerResponse(StatusCodes.Status404NotFound, "If the detection for the given id was not found.")]
        [SwaggerResponse(StatusCodes.Status422UnprocessableEntity, "If the update failed for some reason.")]
        [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
        [Authorize("Moderators")]
        public async ValueTask<ActionResult<ModerateDetectionsResponse>> PutModeratedInfoAsync(
            [FromBody][SwaggerParameter("The moderator-related fields to update.", Required = true)] ModerateDetectionsRequest request)
        {
            try
            {
                var detection = await _detectionOrchestrationService.ModerateDetectionsByIdAsync(request);

                return Ok(detection);
            }
            catch (Exception exception)
            {
                if (exception is DetectionOrchestrationValidationException &&
                    exception.InnerException is NotFoundMetadataException)
                    return NotFound(ValidatorUtilities.GetInnerMessage(exception));

                if (exception is DetectionOrchestrationValidationException &&
                    (exception.InnerException is DetectionNotDeletedException ||
                    exception.InnerException is DetectionNotInsertedException))
                    return UnprocessableEntity(ValidatorUtilities.GetInnerMessage(exception));

                if (exception is DetectionOrchestrationValidationException ||
                    exception is DetectionOrchestrationDependencyValidationException)
                    return BadRequest(ValidatorUtilities.GetInnerMessage(exception));

                if (exception is DetectionOrchestrationDependencyException ||
                    exception is DetectionOrchestrationServiceException)
                    return Problem(exception.Message);

                return Problem(exception.Message);
            }
        }
    }
}
