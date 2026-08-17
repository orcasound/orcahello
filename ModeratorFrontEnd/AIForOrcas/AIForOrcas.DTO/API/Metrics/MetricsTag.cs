using System.Collections.Generic;

namespace AIForOrcas.DTO.API
{
    /// <summary>
    /// Information about entered tag.
    /// </summary>
    public class MetricsTag
    {
        /// <summary>
        /// Tag name.
        /// </summary>
        /// <exampl>CLANG</exampl>
        public string Tag { get; set; }

        /// <summary>
        /// List of detection unique Ids associated with this tag.
        /// </summary>
        /// <example>["00000000-0000-0000-0000-000000000000","00000000-0000-0000-0000-000000000000"]</example>
        public List<string> Ids { get; set; } = new List<string>();
    }
}
