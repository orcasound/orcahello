namespace OrcaHello.Web.UI.Models
{
    [ExcludeFromCodeCoverage]
    public class ModerateDetectionRequest
    {
        public string Id { get; set; } = null!;
        public string State { get; set; } = null!;
        public string Comments { get; set; } = null!;
        public string Moderator { get; set; } = null!;
        public DateTime? Moderated { get; set; }
        public string Tags { get; set; } = null!;
        public string InterestLabel { get; set; } = null!;
    }
}
