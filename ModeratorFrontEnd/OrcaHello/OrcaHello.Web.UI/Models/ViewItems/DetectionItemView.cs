namespace OrcaHello.Web.UI.Models
{
    // This class represents a view model for detection items, which are used throughout the
    // various view services to display information about detections.
    [ExcludeFromCodeCoverage]
    public class DetectionItemView
    {
        // Unique identifier for the detection item.
        public string Id { get; set; } = string.Empty;

        // URI to the audio associated with the detection.
        public string AudioUri { get; set; } = string.Empty;

        // URI to the spectrogram image associated with the detection.
        public string SpectrogramUri { get; set; } = string.Empty;

        // Current state of the detection (e.g., Unreviewed, Positive, Negative, Unknown).
        public string State { get; set; } = string.Empty;

        // Name of the location associated with the detection.
        public string LocationName { get; set; } = string.Empty;

        // Confidence level of the detection.
        public decimal Confidence { get; set; } = decimal.MinValue;

        // Location information associated with the detection.
        public LocationItemView Location { get; set; } = new();

        // List of annotations related to the detection.
        public List<AnnotationItemView> Annotations { get; set; } = new();

        // Timestamp when the detection occurred.
        public DateTime Timestamp { get; set; } = new();

        // Name of the moderator who reviewed the detection.
        public string Moderator { get; set; } = string.Empty;

        // Date and time when the detection was moderated.
        public DateTime? Moderated { get; set; } = new();

        // Moderator comments or notes about the detection.
        public string Comments { get; set; } = string.Empty;

        // Moderator-provided tags associated with the detection.
        public string Tags { get; set; } = string.Empty;

        // Label indicating the level of interest in the detection.
        public string InterestLabel { get; set; } = string.Empty;

        // Indicates whether the audio is currently playing.
        public bool IsCurrentlyPlaying { get; set; } = false;

        // Generates a formatted string representing the average confidence level.
        public string AverageConfidence { get => $"{Confidence.ToString("00.##")}% average confidence"; }

        // Generates a formatted string representing the confidence level.
        public string SmallConfidence { get => $"{Confidence.ToString("F2")}%"; }

        // Generates a string indicating the number of detections.
        public string DetectionCount { get => $"{Annotations.Count} detections"; }

        // Generates a string representation of the detection state.
        public string StateString
        {
            get
            {
                DetectionState stateEnum = (DetectionState)Enum.Parse(typeof(DetectionState), State, true);

                switch (stateEnum)
                {
                    case DetectionState.Unreviewed:
                        return "Unreviewed";
                    case DetectionState.Positive:
                        return "Yes";
                    case DetectionState.Negative:
                        return "No";
                    case DetectionState.Unknown:
                        return "Don't Know";
                    default:
                        return string.Empty;
                }
            }
        }

        // Generates a string representation of the full location (name, latitude, and longitude).
        public string FullLocation
        {
            get
            {
                if (Location is null)
                {
                    return string.Empty;
                }

                return $"{Location.Name} ({Location.Latitude.ToString("00.##")}, {Location.Longitude.ToString("00.##")})";
            }
        }

        // Converts the comma-separated Tags property into a list of strings.
        public List<string> TagsList
        {
            get
            {
                if (string.IsNullOrWhiteSpace(Tags))
                {
                    return new List<string>();
                }

                return Tags.Split(',')
                    .Select(s => s.Trim())
                    .ToList();
            }
            set
            {
                if (value != null && value.Count > 0)
                {
                    Tags = string.Join(',', value);
                }
            }
        }

        // Allows adding and modifying tags through a string input.
        public string EnteredTags
        {
            get
            {
                return string.Empty;
            }
            set
            {
                if (!string.IsNullOrWhiteSpace(value))
                {
                    var enteredTagsList = value
                        .Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                        .Select(s => s.Trim())
                        .ToList();

                    var workingTagsList = !string.IsNullOrWhiteSpace(Tags) ?
                        Tags.Split(',').Select(s => s.Trim()).ToList() : new List<string>();

                    foreach (var tag in enteredTagsList)
                    {
                        if (!workingTagsList.Contains(tag))
                        {
                            workingTagsList.Add(tag);
                        }
                    }

                    if (workingTagsList.Count > 0)
                    {
                        Tags = string.Join(",", workingTagsList);
                    }
                }
            }
        }

        // Converts a Detection object (from the API) into a DetectionItemView using a delegate.
        public static Func<Detection, DetectionItemView> AsDetectionItemView =>
             detection => new DetectionItemView
             {
                 Id = detection.Id,
                 LocationName = detection.LocationName,
                 Timestamp = detection.Timestamp,
                 AudioUri = detection.AudioUri,
                 SpectrogramUri = detection.SpectrogramUri,
                 Confidence = detection.Confidence,
                 State = detection.State,
                 Location = LocationItemView.AsLocationItemView(detection.Location),
                 Comments = detection.Comments,
                 Moderator = detection.Moderator,
                 Moderated = detection.Moderated,
                 Tags = String.Join(", ", detection.Tags),
                 Annotations = detection.Annotations.Select(AnnotationItemView.AsAnnotationItemView).ToList(),
                 InterestLabel = detection.InterestLabel
             };
    }
}