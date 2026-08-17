namespace OrcaHello.Web.Shared.Models.Comments
{
    [ExcludeFromCodeCoverage]
    [SwaggerSchema("Comment-related content for a hydrophone sampling that might contain whale sounds.")]
    public class Comment
    {
        [SwaggerSchema("The detection's generated unique Id.")]
        public string Id { get; set; }

        [SwaggerSchema("Any text comments entered by the human moderator during review.")]
        public string Comments { get; set; }

        [SwaggerSchema("The location of the hydrophone.")]
        public string LocationName { get; set; }

        [SwaggerSchema("Identity of the human moderator (User Principal Name for AzureAD) performing the review.")]
        public string Moderator { get; set; }

        [SwaggerSchema("Date and time of when the detection was reviewed by the human moderator.")]
        public DateTime? Moderated { get; set; }

        [SwaggerSchema("Date and time of when the detection was collected.")]
        public DateTime Timestamp { get; set; }

        [SwaggerSchema("URI of the detection's audio file (.wav) in blob storage.")]
        public string AudioUri { get; set; }

        [SwaggerSchema("URI of the detection's image file (.png) in blob storage.")]
        public string SpectrogramUri { get; set; }

    }
}
