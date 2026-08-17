namespace AIForOrcas.DTO
{
    public class PageLinkDTO
    {
        public PageLinkDTO(int page)
    : this(page, true) { }

        public PageLinkDTO(int page, bool enabled)
            : this(page, enabled, page.ToString())
        { }

        public PageLinkDTO(int page, bool enabled, string text)
        {
            Page = page;
            Enabled = enabled;
            Text = text;
        }

        public string Text { get; set; }
        public int Page { get; set; }
        public bool Enabled { get; set; } = true;
        public bool Active { get; set; } = false;
    }
}
