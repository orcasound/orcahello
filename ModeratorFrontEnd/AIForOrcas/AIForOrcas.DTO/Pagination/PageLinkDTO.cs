namespace AIForOrcas.DTO
{
    /// <summary>
    /// A single page-number link rendered by the pagination control.
    /// </summary>
    public class PageLinkDTO
    {
        /// <summary>
        /// Creates an enabled link labeled with the page number.
        /// </summary>
        /// <param name="page">The page number this link navigates to.</param>
        public PageLinkDTO(int page)
    : this(page, true) { }

        /// <summary>
        /// Creates a link labeled with the page number.
        /// </summary>
        /// <param name="page">The page number this link navigates to.</param>
        /// <param name="enabled">Whether the link can be clicked.</param>
        public PageLinkDTO(int page, bool enabled)
            : this(page, enabled, page.ToString())
        { }

        /// <summary>
        /// Creates a link with custom display text.
        /// </summary>
        /// <param name="page">The page number this link navigates to.</param>
        /// <param name="enabled">Whether the link can be clicked.</param>
        /// <param name="text">The text to display for this link.</param>
        public PageLinkDTO(int page, bool enabled, string text)
        {
            Page = page;
            Enabled = enabled;
            Text = text;
        }

        /// <summary>
        /// The text to display for this link.
        /// </summary>
        public string Text { get; set; }

        /// <summary>
        /// The page number this link navigates to.
        /// </summary>
        public int Page { get; set; }

        /// <summary>
        /// Whether the link can be clicked.
        /// </summary>
        public bool Enabled { get; set; } = true;

        /// <summary>
        /// Whether this link represents the currently selected page.
        /// </summary>
        public bool Active { get; set; } = false;
    }
}
