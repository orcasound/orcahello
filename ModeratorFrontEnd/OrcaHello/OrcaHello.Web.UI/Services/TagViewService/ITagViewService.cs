namespace OrcaHello.Web.UI.Services
{
    public interface ITagViewService
    {
        ValueTask<List<TagItemView>> RetrieveAllTagViewsAsync();
        ValueTask<TagItemViewResponse> DeleteTagAsync(TagItemView tagItem);
        ValueTask<TagItemViewResponse> ReplaceTagAsync(ReplaceTagRequest request);
        ValueTask<DetectionItemViewResponse> RetrieveDetectionsByTagsAsync(PaginatedDetectionsByTagsAndDateRequest request);
    }
}
