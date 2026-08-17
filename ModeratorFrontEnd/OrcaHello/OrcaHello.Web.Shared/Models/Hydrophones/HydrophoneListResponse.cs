namespace OrcaHello.Web.Shared.Models.Hydrophones
{
    [ExcludeFromCodeCoverage]
    [SwaggerSchema("A list of all hydrophone locations.")]
    public class HydrophoneListResponse
    {
        [SwaggerSchema("The list of hydrophones.")]
        public List<Hydrophone> Hydrophones { get; set; } = new List<Hydrophone>();

        [SwaggerSchema("The total number of hydrophones in the list")]
        public int Count { get; set; }
    }
}
