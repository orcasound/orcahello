namespace OrcaHello.Web.Api.Controllers
{
    [Route("api/interestlabels")]
    [SwaggerTag("This controller is responsible for retrieving and curating interest labels submitted by moderators against reviewed detections.")]
    [ApiController]
    public class InterestLabelsController : ControllerBase
    {
        private readonly IInterestLabelOrchestrationService _interestLabelOrchestrationService;

        public InterestLabelsController(IInterestLabelOrchestrationService interestLabelOrchestrationService)
        {
            _interestLabelOrchestrationService = interestLabelOrchestrationService;
        }

        [HttpGet]
        [SwaggerOperation(Summary = "Gets a list of all unique interest labels.")]
        [SwaggerResponse(StatusCodes.Status200OK, "Returns the list of interest labels.", typeof(InterestLabelListResponse))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
        [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
        [AllowAnonymous]
        public async ValueTask<ActionResult<InterestLabelListResponse>> GetAllInterestLabelsAsync()
        {
            try
            {
                var interestLabelListResponse = await _interestLabelOrchestrationService.RetrieveAllInterestLabelsAsync();

                return Ok(interestLabelListResponse);
            }
            catch (Exception exception)
            {
                if (exception is InterestLabelOrchestrationValidationException ||
                    exception is InterestLabelOrchestrationDependencyValidationException)
                {
                    return BadRequest(ValidatorUtilities.GetInnerMessage(exception));
                }

                if (exception is InterestLabelOrchestrationDependencyException ||
                    exception is InterestLabelOrchestrationServiceException)
                {
                    return Problem(exception.Message);
                }

                return Problem(exception.Message);
            }
        }

        [HttpDelete("{id}")]
        [SwaggerOperation(Summary = "Deletes the interest label for the passed detection.")]
        [SwaggerResponse(StatusCodes.Status200OK, "The interest label was removed from the detection.")]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
        [SwaggerResponse(StatusCodes.Status404NotFound, "If the target metadata could not be found to update.")]
        [SwaggerResponse(StatusCodes.Status422UnprocessableEntity, "If the update failed for some reason.")]
        [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
        [Authorize("Moderators")]
        public async ValueTask<ActionResult<InterestLabelRemovalResponse>> RemoveInterestLabelFromDetectionAsync(
            [SwaggerParameter("The detection id.", Required = true)] string id)
        {
            try
            {
                var result = await _interestLabelOrchestrationService.RemoveInterestLabelFromDetectionAsync(id);

                return Ok(result);
            }
            catch (Exception exception)
            {
                if (exception is InterestLabelOrchestrationValidationException &&
                    exception.InnerException is NotFoundMetadataException)
                {
                    return NotFound(ValidatorUtilities.GetInnerMessage(exception));
                }

                if (exception is InterestLabelOrchestrationValidationException &&
                    (exception.InnerException is DetectionNotDeletedException ||
                    exception.InnerException is DetectionNotInsertedException))
                {
                    return UnprocessableEntity(ValidatorUtilities.GetInnerMessage(exception));
                }

                if (exception is InterestLabelOrchestrationValidationException ||
                    exception is InterestLabelOrchestrationDependencyValidationException)
                {
                    return BadRequest(ValidatorUtilities.GetInnerMessage(exception));
                }

                if (exception is InterestLabelOrchestrationDependencyException ||
                    exception is InterestLabelOrchestrationServiceException)
                {
                    return Problem(exception.Message);
                }

                return Problem(exception.Message);
            }
        }

        [HttpPut("{id}")]
        [SwaggerOperation(Summary = "Adds the interest label to the passed detection.")]
        [SwaggerResponse(StatusCodes.Status200OK, "The interest label was added to the detection.")]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
        [SwaggerResponse(StatusCodes.Status404NotFound, "If the target metadata could not be found to update.")]
        [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
        [Authorize("Moderators")]
        public async ValueTask<ActionResult<InterestLabelAddResponse>> AddInterestLabelToDetectionAsync(
            [SwaggerParameter("The detection id.", Required = true)] string id,
            [SwaggerParameter("The interest label to add.", Required = true)] string interestLabel)
        {
            try
            {
                var result = await _interestLabelOrchestrationService.AddInterestLabelToDetectionAsync(id, interestLabel);

                return Ok(result);
            }
            catch (Exception exception)
            {
                if (exception is InterestLabelOrchestrationValidationException &&
                    exception.InnerException is NotFoundMetadataException)
                {
                    return NotFound(ValidatorUtilities.GetInnerMessage(exception));
                }

                if (exception is InterestLabelOrchestrationValidationException ||
                    exception is InterestLabelOrchestrationDependencyValidationException)
                {
                    return BadRequest(ValidatorUtilities.GetInnerMessage(exception));
                }

                if (exception is InterestLabelOrchestrationDependencyException ||
                    exception is InterestLabelOrchestrationServiceException)
                {
                    return Problem(exception.Message);
                }

                return Problem(exception.Message);
            }
        }
    }
}
