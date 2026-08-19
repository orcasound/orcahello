namespace OrcaHello.Web.Api.Controllers
{
    [Route("api/metrics")]
    [SwaggerTag("This controller is responsible for retrieving detection metrics.")]
    [ApiController]
    public class MetricsController : ControllerBase
    {
        private readonly IMetricsOrchestrationService _metricsOrchestrationService;

        public MetricsController(IMetricsOrchestrationService metricsOrchestrationService)
        {
            _metricsOrchestrationService = metricsOrchestrationService;
        }

        [HttpGet]
        [SwaggerOperation(Summary = "Gets the state metrics for the given timeframe.")]
        [SwaggerResponse(StatusCodes.Status200OK, "Returns the state metrics for the given timeframe.", typeof(MetricsResponse))]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
        [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
        [AllowAnonymous]
        public async ValueTask<ActionResult<MetricsResponse>> GetMetricsAsync(
            [SwaggerParameter("The start date of the search (MM/DD/YYYY).", Required = true)] DateTime? fromDate,
            [SwaggerParameter("The end date of the search (MM/DD/YYYY).", Required = true)] DateTime? toDate)
        {
            try
            {
                var metricsResponse = await _metricsOrchestrationService.RetrieveMetricsForGivenTimeframeAsync(fromDate, toDate);

                return Ok(metricsResponse);
            }
            catch (Exception exception)
            {
                if (exception is MetricOrchestrationValidationException ||
                    exception is MetricOrchestrationDependencyValidationException)
                {
                    return BadRequest(ValidatorUtilities.GetInnerMessage(exception));
                }

                if (exception is MetricOrchestrationDependencyException ||
                    exception is MetricOrchestrationServiceException)
                {
                    return Problem(exception.Message);
                }

                return Problem(exception.Message);
            }
        }
    }
}
