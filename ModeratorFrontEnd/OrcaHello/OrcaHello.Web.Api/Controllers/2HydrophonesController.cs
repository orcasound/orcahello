namespace OrcaHello.Web.Api.Controllers
{
    [Route("api/hydrophones")]
    [SwaggerTag("This controller is responsible for retrieving Hydrophone locations.")]
    [ApiController]
    public class HydrophonesController : ControllerBase
    {
        private readonly IHydrophoneOrchestrationService _hydrophoneOrchestrationService;

        public HydrophonesController(IHydrophoneOrchestrationService hydrophoneOrchestrationService)
        {
            _hydrophoneOrchestrationService = hydrophoneOrchestrationService;
        }

        // For pulling and updating hydrophone definitions

        [HttpGet]
        [SwaggerOperation(Summary = "Gets a list of unique hydrophones.")]
        [SwaggerResponse(StatusCodes.Status200OK, "Returns the list of hydrophones.", typeof(HydrophoneListResponse))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
        [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
        [AllowAnonymous]
        public async ValueTask<ActionResult<HydrophoneListResponse>> GetHydrophones()
        {
            try
            {
                var hydrophoneListResponse = await _hydrophoneOrchestrationService.
                    RetrieveHydrophoneLocations();

                return Ok(hydrophoneListResponse);
            }
            catch (Exception exception)
            {
                if (exception is HydrophoneOrchestrationValidationException &&
                    exception.InnerException is InvalidHydrophoneException)
                {
                    return NotFound(ValidatorUtilities.GetInnerMessage(exception));
                }

                if (exception is HydrophoneOrchestrationValidationException ||
                    exception is HydrophoneOrchestrationDependencyValidationException)
                {
                    return BadRequest(ValidatorUtilities.GetInnerMessage(exception));
                }

                if (exception is HydrophoneOrchestrationDependencyException ||
                    exception is HydrophoneOrchestrationServiceException)
                {
                    return Problem(exception.Message);
                }

                return Problem(exception.Message);
            }
        }
    }
}
