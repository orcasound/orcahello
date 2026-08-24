namespace AIForOrcas.Client.Web.Pages.Detections;

public partial class FalsePositives : IDisposable
{
    [Inject]
    IJSRuntime JSRuntime { get; set; }

    [Inject]
    IDetectionService Service { get; set; }

    [Inject]
    IToastService ToastService { get; set; }

    [Inject]
    UserTagCache TagCache { get; set; }

    [Inject]
    AuthenticationStateProvider AuthenticationStateProvider { get; set; }

    private string _userId;
    private List<Detection> detections = null;

    private PaginationOptionsDTO paginationOptions =
        new PaginationOptionsDTO() { RecordsPerPage = 5, Page = 1 };

    private ReviewedFilterOptionsDTO filterOptions =
        new ReviewedFilterOptionsDTO() { SortBy = "timestamp", SortOrder = "desc", Timeframe = "24h", Location = "all" };

    private PaginationResultsDTO pagination = new PaginationResultsDTO();

    private string loadStatus = null;

    protected override async Task OnInitializedAsync()
    {
        await LoadDetections();

        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        _userId = user.FindFirst("oid")?.Value;
    }

    private async Task LoadDetections()
    {
        loadStatus = "Loading records...";
        detections = null;
        var paginatedResponse = await Service.GetFalseDetectionsAsync(paginationOptions, filterOptions);

        pagination.TotalNumberOfRecords = paginatedResponse.TotalNumberRecords;
        pagination.TotalNumberOfPages = paginatedResponse.TotalAmountPages;

        if (pagination.TotalNumberOfPages > 0 && paginationOptions.Page > pagination.TotalNumberOfPages)
        {
            paginationOptions.Page = pagination.TotalNumberOfPages;
            paginatedResponse = await Service.GetFalseDetectionsAsync(paginationOptions, filterOptions);
            pagination.TotalNumberOfRecords = paginatedResponse.TotalNumberRecords;
            pagination.TotalNumberOfPages = paginatedResponse.TotalAmountPages;
        }

        pagination.CurrentPage = paginationOptions.Page;

        if (paginatedResponse.Response == null)
        {
            loadStatus = "An unknown error occurred while loading records...";
        }
        else if (paginatedResponse.Response.Count == 0)
        {
            loadStatus = "No records found for the selected filter options. Please select a different set of filter options...";
        }
        else
        {
            loadStatus = null;
            detections = paginatedResponse.Response;
        }
    }

    private async Task ActOnSelectPageCallback(PaginationOptionsDTO returnedPaginationOptions)
    {
        paginationOptions = returnedPaginationOptions;
        await LoadDetections();
        await JSRuntime.InvokeVoidAsync("DestroyActivePlayer");
        StateHasChanged();
    }

    private async Task ActOnApplyFilterCallback(ReviewedFilterOptionsDTO returnedFilterOptions)
    {
        filterOptions = returnedFilterOptions;
        paginationOptions.Page = 1;
        await LoadDetections();
        await JSRuntime.InvokeVoidAsync("DestroyActivePlayer");
        StateHasChanged();
    }

    private async Task ActOnSubmitCallback(DetectionUpdate request)
    {
        await Service.UpdateRequestAsync(request);

        List<string> leafTags = Detection.GetLeafTags(request.Tags);
        TagCache.SetTags(_userId, leafTags);

        ToastService.ShowSuccess("Detection successfully updated.");

        await LoadDetections();
    }

    void IDisposable.Dispose()
    {
        JSRuntime.InvokeVoidAsync("DestroyActivePlayer");
    }
}
