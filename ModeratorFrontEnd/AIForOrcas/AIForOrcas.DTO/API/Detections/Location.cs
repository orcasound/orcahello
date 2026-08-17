namespace AIForOrcas.DTO.API
{
    /// <summary>
    /// Geographical location of the hydrophone that collected the detection.
    /// </summary>
    public class Location
    {
        /// <summary>
        /// Name of the hydrophone location.
        /// </summary>
        /// <example>Orcasound Lab</example>
        public string Name { get; set; }

        /// <summary>
        /// Longitude of the hydrophone's location.
        /// </summary>
        /// <example>-123.2166658</example>
        public double Longitude { get; set; }

        /// <summary>
        /// Latitude of the hydrophone's location.
        /// </summary>
        /// <example>48.5499978</example>
        public double Latitude { get; set; }
    }
}
