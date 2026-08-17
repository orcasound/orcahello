namespace AIForOrcas.Server.Controllers;

/// <summary>
/// Endpoint for retrieving metrics information about the system and users.
/// </summary>
[Produces("application/json")]
[Route("api/metrics")]
[ApiController]
public class MetricsController : ControllerBase
{
    private readonly MetadataRepository _repository;

    public MetricsController(MetadataRepository repository)
    {
        _repository = repository;
    }

    private IQueryable<Metadata> BuildQueryableAsync(string timeframe, string moderator = null)
    {
        // start with all records
        var queryable = _repository.GetAll();

        // apply timeframe filter
        MetadataFilters.ApplyTimeframeFilter(ref queryable, timeframe);

        // apply moderator filter, if applicable
        MetadataFilters.ApplyModeratorFilter(ref queryable, moderator);

        return queryable;
    }

    /// <summary>
    /// Fetch system metrics.
    /// </summary>
    [HttpGet("system")]
    [AllowAnonymous]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns the system's metrics.", typeof(Metrics))]
    [SwaggerResponse(StatusCodes.Status204NoContent, "If there are no metrics for the specified timeframe.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
    public ActionResult<Metrics> GetSystemMetrics([FromQuery] MetricsFilterDTO queryParameters)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(queryParameters.Timeframe))
                throw new ArgumentNullException("Timeframe");

            var metrics = new Metrics();

            metrics.Timeframe = queryParameters.Timeframe;

            // Build base queryable
            var queryable = BuildQueryableAsync(queryParameters.Timeframe);

            // If not metrics to return
            if (queryable == null || queryable.Count() == 0)
            {
                return NoContent();
            }

            var results = queryable
                .Select(x => DetectionProcessors.ToDetection(x)).ToList();

            // Pull reviewed/unreviewed metrics from querable
            var reviewed = DetectionProcessors.GetReviewed(results);

            metrics.Reviewed = reviewed.ReviewedCount;
            metrics.Unreviewed = reviewed.UnreviewedCount;

            // Pull results metrics from queryable
            var detections = DetectionProcessors.GetResults(results);

            metrics.ConfirmedDetection = detections.ConfirmedCount;
            metrics.FalseDetection = detections.FalseCount;
            metrics.UnknownDetection = detections.UnknownCount;

            // Pull comments from queryable
            metrics.ConfirmedComments = DetectionProcessors.GetComments(results, "yes");
            metrics.UnconfirmedComments = DetectionProcessors.GetComments(results, "no");
            metrics.UnconfirmedComments.AddRange(DetectionProcessors.GetComments(results, "don't know"));
            metrics.UnconfirmedComments = metrics.UnconfirmedComments.OrderByDescending(x => x.Timestamp).ToList();

            // Pull tags from queryable
            metrics.Tags = DetectionProcessors.GetTags(results);

            return Ok(metrics);
        }
        catch (ArgumentNullException ex)
        {
            var details = new ProblemDetails()
            {
                Detail = ex.Message
            };
            return BadRequest(details);
        }
        catch (Exception ex)
        {
            var details = new ProblemDetails()
            {
                Title = ex.GetType().ToString(),
                Detail = ex.Message
            };

            return StatusCode(StatusCodes.Status500InternalServerError, details);
        }
    }

    /// <summary>
    /// Fetch user metrics.
    /// </summary>
    [HttpGet("moderator")]
    [AllowAnonymous]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns the user's metrics.", typeof(Metrics))]
    [SwaggerResponse(StatusCodes.Status204NoContent, "If the user has no activity for the specified timeframe.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
    public ActionResult<Metrics> GetModeratorMetrics([FromQuery] ModeratorMetricsFilterDTO queryParameters)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(queryParameters.Timeframe))
                throw new ArgumentNullException("Timeframe");

            if (string.IsNullOrWhiteSpace(queryParameters.Moderator))
                throw new ArgumentNullException("Moderator");

            var metrics = new ModeratorMetrics();

            metrics.Timeframe = queryParameters.Timeframe;
            metrics.Moderator = queryParameters.Moderator;

            // Build base queryable
            var queryable = BuildQueryableAsync(queryParameters.Timeframe, queryParameters.Moderator);

            // If not metrics to return
            if (queryable == null || queryable.Count() == 0)
            {
                return NoContent();
            }

            var results = queryable
                .Select(x => DetectionProcessors.ToDetection(x)).ToList();

            //// Pull reviewed/unreviewed metrics from querable
            var reviewed = DetectionProcessors.GetReviewed(results);

            metrics.Reviewed = reviewed.ReviewedCount;
            metrics.Unreviewed = reviewed.UnreviewedCount;

            // Pull results metrics from queryable
            var detections = DetectionProcessors.GetResults(results);

            metrics.ConfirmedDetection = detections.ConfirmedCount;
            metrics.FalseDetection = detections.FalseCount;
            metrics.UnknownDetection = detections.UnknownCount;

            // Pull comments from queryable
            metrics.ConfirmedComments = DetectionProcessors.GetComments(results, "yes");
            metrics.UnconfirmedComments = DetectionProcessors.GetComments(results, "no");
            metrics.UnconfirmedComments.AddRange(DetectionProcessors.GetComments(results, "don't know"));
            metrics.UnconfirmedComments = metrics.UnconfirmedComments.OrderByDescending(x => x.Timestamp).ToList();

            // Pull tags from queryable
            metrics.Tags = DetectionProcessors.GetTags(results);

            return Ok(metrics);
        }
        catch (ArgumentNullException ex)
        {
            var details = new ProblemDetails()
            {
                Detail = ex.Message
            };
            return BadRequest(details);
        }
        catch (Exception ex)
        {
            var details = new ProblemDetails()
            {
                Title = ex.GetType().ToString(),
                Detail = ex.Message
            };

            return StatusCode(StatusCodes.Status500InternalServerError, details);
        }
    }
}