using System.ComponentModel.DataAnnotations;

namespace AIForOrcas.DTO.API.Tags
{
    /// <summary>
    /// Used to pass information regarding updating a Tag
    /// </summary>
    public class TagUpdate
    {
        /// <summary>
        /// The Tag being changed
        /// </summary>
        /// <example>OldTag</example>
        [Required]
        public string OldTag { get; set; }
        /// <summary>
        /// What the Tag is being change to
        /// </summary>
        /// <example>NewTag</example>
        [Required(ErrorMessage = "Please enter the new tag.")]
        public string NewTag { get; set; }
    }
}
