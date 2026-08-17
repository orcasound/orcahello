namespace OrcaHello.Web.Shared.Models.Detections
{
    [ExcludeFromCodeCoverage]
    public class DetectionListResponseBase
    {
        [SwaggerSchema("A paginated list of Detections for the given filter information.")]
        public List<Detection> Detections { get; set; }

        [SwaggerSchema("The total number of detections in the list (for pagination).")]
        public int TotalCount { get; set; }
    }

    [ExcludeFromCodeCoverage]
    [SwaggerSchema("List of Detections by timeframe and tag.")]
    public class DetectionListForTagResponse : DetectionListResponseBase
    {
        [SwaggerSchema("The starting date of the timeframe.")]
        public DateTime FromDate { get; set; }

        [SwaggerSchema("The ending date of the timeframe.")]
        public DateTime ToDate { get; set; }

        [SwaggerSchema("The filtering tag.")]
        public string Tag { get; set; }

        [SwaggerSchema("The number of detections for this page.")]
        public int Count { get; set; }

        [SwaggerSchema("The requested page number.")]
        public int Page { get; set; }

        [SwaggerSchema("The requested page size.")]
        public int PageSize { get; set; }
    }

    [ExcludeFromCodeCoverage]
    [SwaggerSchema("List of Detections by interest label.")]
    public class DetectionListForInterestLabelResponse : DetectionListResponseBase
    {
        [SwaggerSchema("The filtering interest label.")]
        public string InterestLabel { get; set; }
    }

    [ExcludeFromCodeCoverage]
    [SwaggerSchema("Sorted list of Detections by state, timeframe, location.")]
    public class DetectionListResponse : DetectionListResponseBase
    {
        [SwaggerSchema("The starting date of the timeframe.")]
        public DateTime FromDate { get; set; }

        [SwaggerSchema("The ending date of the timeframe.")]
        public DateTime ToDate { get; set; }

        [SwaggerSchema("The state of the detections.")]
        public string State { get; set; }

        [SwaggerSchema("The location of the detections.")]
        public string Location { get; set; }

        [SwaggerSchema("The sort by property.")]
        public string SortBy { get; set; }

        [SwaggerSchema("The sort order.")]
        public string SortOrder { get; set; }

        [SwaggerSchema("The number of detections for this page.")]
        public int Count { get; set; }

        [SwaggerSchema("The requested page number.")]
        public int Page { get; set; }

        [SwaggerSchema("The requested page size.")]
        public int PageSize { get; set; }

    }
}
