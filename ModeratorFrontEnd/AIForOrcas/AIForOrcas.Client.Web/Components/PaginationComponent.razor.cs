namespace AIForOrcas.Client.Web.Components;

public partial class PaginationComponent
{
    [Parameter]
    public PaginationOptionsDTO PaginationOptions { get; set; }

    [Parameter]
    public PaginationResultsDTO PaginationResults { get; set; }

    [Parameter]
    public EventCallback<PaginationOptionsDTO> SelectPageCallback { get; set; }

    List<PageLinkDTO> links;

    protected override void OnParametersSet()
    {
        BuildPaginationLinks();
    }

    private async Task SelectPage(PageLinkDTO link)
    {
        if (link.Page == PaginationResults.CurrentPage || !link.Enabled)
        {
            return;
        }

        PaginationOptions.Page = link.Page;
        await SelectPageCallback.InvokeAsync(PaginationOptions);
    }

    private void BuildPaginationLinks()
    {
        links = new List<PageLinkDTO>();
        var isPreviousPageLinkEnabled = PaginationResults.CurrentPage != 1;
        var previousPage = PaginationResults.CurrentPage - 1;
        links.Add(new PageLinkDTO(previousPage, isPreviousPageLinkEnabled, "Previous"));

        for (int i = 1; i <= PaginationResults.TotalNumberOfPages; i++)
        {
            if (i == 1 ||
                i == PaginationResults.TotalNumberOfPages ||
                (i >= PaginationResults.CurrentPage - PaginationOptions.Radius &&
                 i <= PaginationResults.CurrentPage + PaginationOptions.Radius))
            {
                links.Add(new PageLinkDTO(i) { Active = PaginationResults.CurrentPage == i });
            }
        }

        var isNextPageLinkEnabled = PaginationResults.TotalNumberOfPages > 0
            && PaginationResults.CurrentPage != PaginationResults.TotalNumberOfPages;
        var nextPage = PaginationResults.CurrentPage + 1;

        links.Add(new PageLinkDTO(nextPage, isNextPageLinkEnabled, "Next"));
    }
}

