namespace OrcaHello.Web.Api.Controllers
{
    [Route("api/home")]
    [SwaggerTag("This controller is responsible for indicating API status.")]
    [ApiController]
    public class HomeController : ControllerBase
    {
        [HttpGet]
        [SwaggerOperation(Summary = "Indicates if the API is operational.")]
        [SwaggerResponse(StatusCodes.Status200OK, "Indicates the API is operational.")]

        [AllowAnonymous]
        [ExcludeFromCodeCoverage]
        public ActionResult<string> Get() =>
            Ok("Welcome to the OrcaHello API v2.0!");

        [HttpGet("moderator")]
        [SwaggerOperation(Summary = "Indicates if the API is operational and the user is properly logged in as a Moderator.")]
        [SwaggerResponse(StatusCodes.Status200OK, "Indicates the API is operational and the user is logged in.")]
        [SwaggerResponse(StatusCodes.Status400BadRequest, "If the request was malformed (missing parameters).")]
        [SwaggerResponse(StatusCodes.Status401Unauthorized, "If the user is not a verified moderator.")]
        [SwaggerResponse(StatusCodes.Status500InternalServerError, "If there is an internal error reading or processing data from the data source.")]
        [Authorize("Moderators")]
        [ExcludeFromCodeCoverage]
        public ActionResult<string> GetLoggedIn() =>
            Ok("Welcome to the OrcaHello API v2.0! You are logged in as a Moderator.");
    }
}
