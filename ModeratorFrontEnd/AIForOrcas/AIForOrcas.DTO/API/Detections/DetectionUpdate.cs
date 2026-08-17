using System;

namespace AIForOrcas.DTO.API
{
    /// <summary>
    /// Detection data to be updated.
    /// </summary>
    public class DetectionUpdate
    {
        /// <summary>
        /// The detection's unique ID.
        /// </summary>
        /// <example>AAAAAAAA-AAAA-AAAA-AAAA-AAAAAAAAAAAA</example>
        public string Id { get; set; }

        /// <summary>
        /// Comments provided by the human moderator.
        /// </summary>
        /// <example>Didn't hear anything of interest.</example>
        public string Comments { get; set; }

        /// <summary>
        /// Tags provided by the human moderator (separated by semi-colon)
        /// </summary>
        /// <example>call;snr-medium</example>
        public string Tags { get; set; }

        /// <summary>
        /// Identity of the human moderator (User Principal Name for AzureAD) reviewing the detection.
        /// </summary>
        /// <example>live.com#user@gmail.com</example>
        public string Moderator { get; set; }

        /// <summary>
        /// Date and time when the detection was moderated.
        /// </summary>
        /// <example>2020-11-21T16:52:45Z</example>
        public DateTime Moderated { get; set; }

        /// <summary>
        /// Flag indicating whether or not the detection has been reviewed.
        /// </summary>
        /// <example>true</example>
        public bool Reviewed { get; set; }

        /// <summary>
        /// Indicates whether whale sounds were heard in the detection (yes, no, don't know).
        /// </summary>
        /// <example>no</example>
        public string Found { get; set; }
    }
}
