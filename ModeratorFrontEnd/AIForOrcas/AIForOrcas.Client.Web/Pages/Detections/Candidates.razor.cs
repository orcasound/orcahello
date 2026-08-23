namespace AIForOrcas.Client.Web.Pages.Detections;

public partial class Candidates : IDisposable
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

    private CandidateFilterOptionsDTO filterOptions =
        new CandidateFilterOptionsDTO() { SortBy = "timestamp", SortOrder = "desc", Timeframe = "6h", Location = "all", HydrophoneId = "all" };

    private PaginationResultsDTO pagination = new PaginationResultsDTO();

    private string loadStatus = null;

    // Set after a submit; consumed once the re-rendered list is in the DOM.
    private string _scrollToDetectionId;

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
        var paginatedResponse = await Service.GetCandidateDetectionsAsync(paginationOptions, filterOptions);

        pagination.TotalNumberOfRecords = paginatedResponse.TotalNumberRecords;
        pagination.TotalNumberOfPages = paginatedResponse.TotalAmountPages;

        // The page we requested may no longer exist.
        // Clamp to the actual last valid page. re-fetch to render real data
        if (pagination.TotalNumberOfPages > 0 && paginationOptions.Page > pagination.TotalNumberOfPages)
        {
            paginationOptions.Page = pagination.TotalNumberOfPages;
            paginatedResponse = await Service.GetCandidateDetectionsAsync(paginationOptions, filterOptions);
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
            loadStatus = pagination.TotalNumberOfRecords == 0
                ? "You're caught up, no records match the selected filter options..."
                : "No records found for the selected filter options. Please select a different set of filter options...";
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

    private async Task ActOnApplyFilterCallback(CandidateFilterOptionsDTO returnedFilterOptions)
    {
        filterOptions = returnedFilterOptions;
        paginationOptions.Page = 1;
        await LoadDetections();
        await JSRuntime.InvokeVoidAsync("DestroyActivePlayer");
        StateHasChanged();
    }

    private async Task ActOnSubmitCallback(DetectionUpdate request)
    {
        // The candidate that takes the submitted card's place is the next one to
        // moderate; remember where it will be before the list reloads.
        int submittedIndex = detections?.FindIndex(d => d.Id == request.Id) ?? -1;
        int pageBefore = paginationOptions.Page;

        await Service.UpdateRequestAsync(request);

        List<string> leafTags = Detection.GetLeafTags(request.Tags);
        TagCache.SetTags(_userId, leafTags);

        ToastService.ShowSuccess("Detection successfully updated.");

        await JSRuntime.InvokeVoidAsync("DestroyActivePlayer");
        await LoadDetections();

        if (submittedIndex >= 0 && detections != null && detections.Count > 0)
        {
            // Same page: the card that moved up into the submitted slot (or the
            // last one, if that slot is gone). A different page: start at its top.
            int nextIndex = paginationOptions.Page == pageBefore
                ? Math.Min(submittedIndex, detections.Count - 1)
                : 0;
            _scrollToDetectionId = detections[nextIndex].Id;
        }
    }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (_scrollToDetectionId != null)
        {
            string detectionId = _scrollToDetectionId;
            _scrollToDetectionId = null;
            await JSRuntime.InvokeVoidAsync("ScrollCardIntoView", detectionId);
        }

        await base.OnAfterRenderAsync(firstRender);
    }

    void IDisposable.Dispose()
    {
        JSRuntime.InvokeVoidAsync("DestroyActivePlayer");
    }

}
