namespace OrcaHello.Web.UI.Models
{
    [ExcludeFromCodeCoverage]
    public class CommentStateView
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public int Page { get; set; } = 1;
        public int PageSize { get; set; } = 10;
        public bool IsExpanded { get; set; } = false;
        public bool IsLoading { get; set; } = false;
        public List<CommentItemView> Items { get; set; } = null!;
        public int Count { get; set; }

        public void Toggle()
        {
            IsExpanded = !IsExpanded;

            if (IsExpanded && Items == null)
            {
                Items = new();
                Page = 1;
            }
        }

        public void Reset(DateTime fromDate, DateTime toDate)
        {
            Page = 1;
            IsExpanded = false;
            IsLoading = false;
            Items = null!;
            Count = 0;
            FromDate = fromDate;
            ToDate = toDate;
        }

        public void Close()
        {
            Page = 1;
            IsExpanded = false;
            IsLoading = false;
            Items = null!;
            Count = 0;
        }

        public bool IsPopulated => Items != null && Items.Any();

        public bool IsEmpty => Items != null && !Items.Any();
    }
}
