namespace AIForOrcas.DTO.API
{
    /// <summary>
    /// Section within the detection that might contain whale sounds.
    /// </summary>
    public class Annotation
    {
        /// <summary>
        /// Unique identifier (within the detection) of the annotation.
        /// </summary>
        /// <example>1</example>
        public int Id { get; set; }

        /// <summary>
        /// Start time (within the detection) of the annotation as measured in seconds.
        /// </summary>
        /// <example>35</example>
        public decimal StartTime { get; set; }

        /// <summary>
        /// End time (within the detection) of the annotation as measured in seconds.
        /// </summary>
        /// <example>37.5</example>
        public decimal EndTime { get; set; }

        /// <summary>
        /// Calculated confidence that the annotation contains a whale sound.
        /// </summary>
        /// <example>84.39</example>
        public decimal Confidence { get; set; }

        /// <summary>
        /// Predicted label, if any.
        /// </summary>
        public string Label { get; set; } = string.Empty;
    }
}
