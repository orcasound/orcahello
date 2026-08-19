namespace AIForOrcas.Server.Controllers;

/// <summary>
/// Endpoint for retrieving and updating detection items.
/// </summary>
[Produces("application/json")]
[Route("api/detections")]
[ApiController]
public class DetectionsController : ControllerBase
{
    private readonly MetadataRepository _repository;

    public DetectionsController(MetadataRepository repository)
    {
        _repository = repository;
    }

    #region Helpers

    private void SetHeaderCounts(double totalRecords, int recordsPerPage)
    {
        double totalAmountPages = Math.Ceiling(totalRecords / recordsPerPage);

        HttpContext.Response.Headers.Add("totalNumberRecords", totalRecords.ToString());
        HttpContext.Response.Headers.Add("totalAmountPages", totalAmountPages.ToString());
    }

    private static void ValidateQueryParameters(DetectionQueryParameters queryParameters)
    {
        if (string.IsNullOrWhiteSpace(queryParameters.Timeframe))
        {
            throw new ArgumentNullException("Timeframe");
        }

        if (queryParameters.DateFrom > queryParameters.DateTo)
        {
            throw new ArgumentOutOfRangeException("DateFrom", "From Date should be less than To date");
        }

        if (string.IsNullOrWhiteSpace(queryParameters.SortBy))
        {
            throw new ArgumentNullException("SortBy");
        }

        if (string.IsNullOrWhiteSpace(queryParameters.SortOrder))
        {
            throw new ArgumentNullException("SortOrder");
        }

        if (string.IsNullOrWhiteSpace(queryParameters.Location))
        {
            throw new ArgumentNullException("Location");
        }

        if (queryParameters.Page == 0)
        {
            throw new ArgumentNullException("Page");
        }

        if (queryParameters.RecordsPerPage == 0)
        {
            throw new ArgumentNullException("RecordsPerPage");
        }
    }

    private static void ApplyOptionalLocationAndHydrophoneFilters(ref IQueryable<Metadata> queryable, DetectionQueryParameters queryParameters)
    {
        if (queryParameters.Location.ToLower() != "all")
        {
            MetadataFilters.ApplyLocationFilter(ref queryable, queryParameters.Location);
        }

        if (queryParameters.HydrophoneId.ToLower() != "all")
        {
            MetadataFilters.ApplyHydrophoneIdFilter(ref queryable, queryParameters.HydrophoneId);
        }
    }

    private void ApplySortPaginationAndHeaders(ref List<Detection> results, double recordCount, DetectionQueryParameters queryParameters)
    {
        if (queryParameters.SortBy.ToLower() == "confidence")
        {
            DetectionFilters.ApplyConfidenceSortFilter(ref results, queryParameters.SortOrder);
        }
        else if (queryParameters.SortBy.ToLower() == "timestamp")
        {
            DetectionFilters.ApplyTimestampSortFilter(ref results, queryParameters.SortOrder);
        }

        DetectionFilters.ApplyPaginationFilter(ref results, queryParameters.Page, queryParameters.RecordsPerPage);

        SetHeaderCounts(recordCount,
            (queryParameters.RecordsPerPage > 0 ? queryParameters.RecordsPerPage :
                MetadataFilters.DefaultRecordsPerPage));
    }

    #endregion

    /// <summary>
    /// List all AI/ML generated detections, regardless of review status.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns the list of all Detections.", typeof(IQueryable<Detection>))]
    [SwaggerResponse(StatusCodes.Status204NoContent, "If there are no Detections for the specified timeframe.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
    public ActionResult<IQueryable<Detection>> Get([FromQuery] DetectionQueryParameters queryParameters)
    {
        try
        {
            ValidateQueryParameters(queryParameters);

            // start with all records
            var queryable = _repository.GetAll();

            // apply timeframe filter
            MetadataFilters.ApplyTimeframeFilter(ref queryable, queryParameters.Timeframe, queryParameters.DateFrom, queryParameters.DateTo);

            // apply location and hydrophone filters
            ApplyOptionalLocationAndHydrophoneFilters(ref queryable, queryParameters);

            // If no detections found
            if (queryable == null || queryable.Count() == 0)
            {
                return NoContent();
            }

            // total number of records
            double recordCount = queryable.Count();

            var results = queryable
                .Select(x => DetectionProcessors.ToDetection(x)).ToList();

            // apply sort, pagination filters and set page count headers
            ApplySortPaginationAndHeaders(ref results, recordCount, queryParameters);

            // map to returnable data type and return
            return Ok(results);
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
    /// Fetch a specific detection based on a unique ID.
    /// </summary>
    /// <param name="id">Detection's unique ID</param>
    [HttpGet("{id}")]
    [AllowAnonymous]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns the Detection.", typeof(Detection))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "If the Detection defined by the unique ID could not be found.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
    public async ValueTask<ActionResult<Detection>> GetByIdAsync(string id)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException("id");
            }

            var metadata = await _repository.GetByIdAsync(id);

            if (metadata == null)
            {
                return NotFound();
            }

            return Ok(DetectionProcessors.ToDetection(metadata));
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
    /// List all AI/ML generated detections that have not yet been reviewed (confirmed or rejected) by a human moderator.
    /// </summary>
    [HttpGet("unreviewed")]
    [AllowAnonymous]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns the list of unreviewed Detections.", typeof(IQueryable<Detection>))]
    [SwaggerResponse(StatusCodes.Status204NoContent, "If there are no unreviewed Detections for the specified timeframe.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
    public ActionResult<IQueryable<Detection>> GetUnreviewed([FromQuery] DetectionQueryParameters queryParameters)
    {
        try
        {
            ValidateQueryParameters(queryParameters);

            // start with all records
            var queryable = _repository.GetAll();

            // apply reviewed status
            MetadataFilters.ApplyReviewedFilter(ref queryable, false);

            // apply location and hydrophone filters
            ApplyOptionalLocationAndHydrophoneFilters(ref queryable, queryParameters);

            // apply timeframe filter
            MetadataFilters.ApplyTimeframeFilter(ref queryable, queryParameters.Timeframe, queryParameters.DateFrom, queryParameters.DateTo);

            // If no detections found
            if (queryable == null || queryable.Count() == 0)
            {
                return NoContent();
            }

            // total number of records
            double recordCount = queryable.Count();

            var results = queryable
                .Select(x => DetectionProcessors.ToDetection(x)).ToList();

            // NOTE: Have to apply SortBy timestamp and pagination filter after
            //       executing the select because of how EF for Cosmos deals with DateTime.
            //       Had to convert from string (how stored in Cosmos) to DateTime in order to apply the
            //       select, but that messed up the SortBy since Cosmos is expecting a string.

            // apply sort, pagination filters and set page count headers
            ApplySortPaginationAndHeaders(ref results, recordCount, queryParameters);

            // map to returnable data type and return
            return Ok(results);
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
    /// List all AI/ML generated detections that have been reviewed by a human moderator and have confirmed whale sounds.
    /// </summary>
    [HttpGet("confirmed")]
    [AllowAnonymous]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns the list of confirmed Detections.", typeof(IQueryable<Detection>))]
    [SwaggerResponse(StatusCodes.Status204NoContent, "If there are no confirmed Detections for the specified timeframe.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
    public ActionResult<IQueryable<Detection>> GetConfirmed([FromQuery] DetectionQueryParameters queryParameters)
    {
        try
        {
            ValidateQueryParameters(queryParameters);

            // start with all records
            var queryable = _repository.GetAll();

            // apply desired status
            MetadataFilters.ApplyReviewedFilter(ref queryable, true);

            // apply desired found state
            MetadataFilters.ApplyFoundFilter(ref queryable, "yes");

            // apply timeframe filter
            MetadataFilters.ApplyTimeframeFilter(ref queryable, queryParameters.Timeframe, queryParameters.DateFrom, queryParameters.DateTo);

            // apply location and hydrophone filters
            ApplyOptionalLocationAndHydrophoneFilters(ref queryable, queryParameters);

            // If no detections found
            if (queryable == null || queryable.Count() == 0)
            {
                return NoContent();
            }

            // total number of records
            double recordCount = queryable.Count();

            var results = queryable
                .Select(x => DetectionProcessors.ToDetection(x)).ToList();

            // apply sort, pagination filters and set page count headers
            ApplySortPaginationAndHeaders(ref results, recordCount, queryParameters);

            // map to returnable data type and return
            return Ok(results);
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
    /// List all AI/ML generated detections that have been reviewed by a human moderator, but do not have whale sounds.
    /// </summary>
    [HttpGet("falsepositives")]
    [AllowAnonymous]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns the list of false positive (unconfirmed) Detections.", typeof(IQueryable<Detection>))]
    [SwaggerResponse(StatusCodes.Status204NoContent, "If there are no false positive Detections for the specified timeframe.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
    public ActionResult<IQueryable<Detection>> GetFalsePositives([FromQuery] DetectionQueryParameters queryParameters)
    {
        try
        {
            ValidateQueryParameters(queryParameters);

            // start with all records
            var queryable = _repository.GetAll();

            // apply desired status
            MetadataFilters.ApplyReviewedFilter(ref queryable, true);

            // apply desired found state
            MetadataFilters.ApplyFoundFilter(ref queryable, "no");

            // apply timeframe filter
            MetadataFilters.ApplyTimeframeFilter(ref queryable, queryParameters.Timeframe, queryParameters.DateFrom, queryParameters.DateTo);

            // apply location and hydrophone filters
            ApplyOptionalLocationAndHydrophoneFilters(ref queryable, queryParameters);

            // If no detections found
            if (queryable == null || queryable.Count() == 0)
            {
                return NoContent();
            }

            // total number of records
            double recordCount = queryable.Count();

            var results = queryable
                .Select(x => DetectionProcessors.ToDetection(x)).ToList();

            // apply sort, pagination filters and set page count headers
            ApplySortPaginationAndHeaders(ref results, recordCount, queryParameters);

            // map to returnable data type and return
            return Ok(results);
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
    /// List all AI/ML generated detections that have been reviewed by a human moderator, but whale sounds could not be conclusively confirmed or denied.
    /// </summary>
    [HttpGet("unknowns")]
    [AllowAnonymous]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns the list of unknown Detections.", typeof(IQueryable<Detection>))]
    [SwaggerResponse(StatusCodes.Status204NoContent, "If there are no unknown Detections for the specified timeframe.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
    public ActionResult<IQueryable<Detection>> GetUnknowns([FromQuery] DetectionQueryParameters queryParameters)
    {
        try
        {
            ValidateQueryParameters(queryParameters);

            // start with all records
            var queryable = _repository.GetAll();

            // apply desired status
            MetadataFilters.ApplyReviewedFilter(ref queryable, true);

            // apply desired found state
            MetadataFilters.ApplyFoundFilter(ref queryable, "don't know");

            // apply timeframe filter
            MetadataFilters.ApplyTimeframeFilter(ref queryable, queryParameters.Timeframe, queryParameters.DateFrom, queryParameters.DateTo);

            // apply location and hydrophone filters
            ApplyOptionalLocationAndHydrophoneFilters(ref queryable, queryParameters);

            // If no detections found
            if (queryable == null || queryable.Count() == 0)
            {
                return NoContent();
            }

            // total number of records
            double recordCount = queryable.Count();

            var results = queryable
                .Select(x => DetectionProcessors.ToDetection(x)).ToList();

            // apply sort, pagination filters and set page count headers
            ApplySortPaginationAndHeaders(ref results, recordCount, queryParameters);

            // map to returnable data type and return
            return Ok(results);
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
    /// Updates the detection with information provided by a human moderator.
    /// </summary>
    /// <param name="id">Detection's unique Id (AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA).</param>
    /// <param name="detectionUpdate">The Detection's values to be updated.</param>
    [HttpPut("{id}")]
    [Authorize("Moderators")]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns the contents of the updated Detection.", typeof(Detection))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "If the user is not logged in.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "If the user is logged in, but is not an authorized Moderator.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Indicates the Detection was not found to update.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
    public async ValueTask<ActionResult<Detection>> Put(string id, [FromBody] DetectionUpdate detectionUpdate)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(id))
            {
                throw new ArgumentNullException("id");
            }

            if (detectionUpdate == null)
            {
                throw new ArgumentNullException("postedDetection");
            }

            var metadata = await _repository.GetByIdAsync(id);

            if (metadata == null)
            {
                return NotFound();
            }

            metadata.comments = detectionUpdate.Comments;
            metadata.moderator = detectionUpdate.Moderator;
            metadata.dateModerated = detectionUpdate.Moderated.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
            metadata.reviewed = detectionUpdate.Reviewed;
            metadata.SRKWFound = (string.IsNullOrWhiteSpace(detectionUpdate.Found)) ? "no" : detectionUpdate.Found.ToLower();

            // Normalize the tags
            if (!string.IsNullOrWhiteSpace(detectionUpdate.Tags))
            {
                var working = detectionUpdate.Tags.Replace(",", ";");

                var tagList = new List<string>();
                tagList.AddRange(working.Split(';').ToList().Select(x => x.Trim()));
                metadata.tags = string.Join(";", tagList);
            }
            else
            {
                metadata.tags = string.Empty;
            }

            await _repository.CommitAsync();

            return Ok(DetectionProcessors.ToDetection(metadata));
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
