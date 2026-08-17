using System;

namespace AIForOrcas.DTO.API
{
    /// <summary>
    /// Information about entered comment.
    /// </summary>
    public class MetricsComment
    {
        /// <summary>
        /// The text of the comment.
        /// </summary>
        /// <example>This is the thing the Moderator said about the detection.</example>
        public string Comment { get; set; }

        /// <summary>
        /// The detection's unique Id.
        /// </summary>
        /// <example>00000000-0000-0000-0000-000000000000</example>
        public string Id { get; set; }

        /// <summary>
        /// Date and time when the comment was submitted.
        /// </summary>
        /// <example>2020-11-19T13:42:32.473918Z</example>
        public DateTime Timestamp { get; set; }

        /// <summary>
        /// Identity of the human moderator (User Principal Name for AzureAD) submitting the comment.
        /// </summary>
        /// <example>live.com#user@gmail.com</example>
        public string Moderator { get; set; }
    }
}
