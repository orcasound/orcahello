namespace AIForOrcas.Server.Controllers;

[Produces("application/json")]
[Route("api/tags")]
[ApiController]
public class TagsController : ControllerBase
{
    private readonly MetadataRepository _repository;

    public TagsController(MetadataRepository repository)
    {
        _repository = repository;
    }

    /// <summary>
    /// Gets a unique list of Tags.
    /// </summary>
    [HttpGet]
    [AllowAnonymous]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns the list of unique Tags.", typeof(List<string>))]
    [SwaggerResponse(StatusCodes.Status204NoContent, "If there were no Tags to return.")]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
    public ActionResult<List<string>> GetAll()
    {
        try
        {
            var rawTags = _repository.GetAllTags().ToList();

            if (rawTags == null || rawTags.Count() == 0)
            {
                return NoContent();
            }

            var uniqueTags = new List<string>();

            rawTags.ForEach(x => uniqueTags.AddRange(x.Split(";")));

            uniqueTags = uniqueTags.Distinct().OrderBy(x => x).ToList();

            return Ok(uniqueTags);
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
    /// Updates all occurrences of the old Tag with the new Tag.
    /// </summary>
    /// <param name="tagUpdate">Tag update payload.</param>
    [HttpPut]
    [Authorize("Moderators")]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns the number of records updated.", typeof(int))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "If the user is not logged in.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "If the user is logged in, but is not an authorized Moderator.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Indicates there were no matching records to update.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
    public async ValueTask<ActionResult<int>> Put([FromBody] TagUpdate tagUpdate)
    {
        try
        {
            if (tagUpdate == null)
            {
                throw new ArgumentNullException("tagUpdate");
            }

            var detectionsToUpdate = _repository.GetAllWithTag(tagUpdate.OldTag).ToList();

            if (detectionsToUpdate.Count() == 0)
            {
                return NoContent();
            }

            foreach (var detection in detectionsToUpdate)
            {
                detection.tags = detection.tags.Replace(tagUpdate.OldTag, tagUpdate.NewTag);
            }

            await _repository.CommitAsync();

            return Ok(detectionsToUpdate.Count());
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
    /// Delete a tag from the database.
    /// </summary>
    /// <param name="tag">Tag to delete (i.e. TagName).</param>
    [HttpDelete]
    [Authorize("Moderators")]
    [SwaggerResponse(StatusCodes.Status200OK, "Returns the number of records updated.", typeof(int))]
    [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
    [SwaggerResponse(StatusCodes.Status401Unauthorized, "If the user is not logged in.")]
    [SwaggerResponse(StatusCodes.Status403Forbidden, "If the user is logged in, but is not an authorized Moderator.")]
    [SwaggerResponse(StatusCodes.Status404NotFound, "Indicates there were no matching records to update.")]
    [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
    public async ValueTask<ActionResult<int>> Delete(string tag)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(tag))
            {
                throw new ArgumentNullException("tag");
            }

            var detectionsToUpdate = _repository.GetAllWithTag(tag).ToList();

            if (detectionsToUpdate.Count() == 0)
            {
                return NoContent();
            }

            foreach (var detection in detectionsToUpdate)
            {
                var split = detection.tags.Split(";").ToList();
                split.Remove(tag);
                detection.tags = string.Join(";", split);
            }

            await _repository.CommitAsync();

            return Ok(detectionsToUpdate.Count());
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
