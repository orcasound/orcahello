namespace OrcaHello.Web.UI.Services
{
    public interface ICommentService
    {
        ValueTask<CommentListResponse> RetrieveFilteredPositiveCommentsAsync(DateTime? fromDate, DateTime? toDate, int page, int pageSize);
        ValueTask<CommentListResponse> RetrieveFilteredNegativeAndUnknownCommentsAsync(DateTime? fromDate, DateTime? toDate, int page, int pageSize);
    }
}
