namespace AIForOrcas.Client.Web.Pages.User;

public partial class UserActivity
{
    [Inject]
    IMetricsService Service { get; set; }

    [Inject]
    IJSRuntime JSRuntime { get; set; }

    [Inject]
    //AuthenticationStateProvider AuthenticationStateProvider { get; set; }
    IAccountService AccountService { get; set; }

    private ModeratorMetrics metrics = null;

    private ModeratorMetricsFilterDTO filterOptions =
        new ModeratorMetricsFilterDTO() { Timeframe = "1m" };

    private string messageStyle = "d-none";
    private string message = string.Empty;
    private string displayStyle = "d-none";

    protected override async Task OnInitializedAsync()
    {
        await LoadMetrics();
    }

    private async Task LoadMetrics()
    {
        filterOptions.Moderator = await AccountService.GetUsername();

        displayStyle = "d-none";
        messageStyle = "";
        message = "Loading metrics...";

        metrics = await Service.GetModeratorMetricsAsync(filterOptions);

        if (!metrics.HasContent)
        {
            message = "No metrics found for the selected filter options. Please select a different set of filter options...";
            displayStyle = "d-none";
        }
        else
        {
            messageStyle = "d-none";
            displayStyle = "";
        }

        StateHasChanged();

        // Both charts live inside the section that stays hidden when there is no content,
        // so drawing them then renders an all-zero doughnut nobody can see.
        if (metrics.HasContent)
        {
            await JSRuntime.InvokeVoidAsync("DrawDetectionsChart", metrics.DetectionsArray);
            await JSRuntime.InvokeVoidAsync("DrawDetectionResultsChart", metrics.DetectionResultsArray);
        }
    }

    private async Task ActOnApplyFilterCallback(MetricsFilterDTO returnedFilterOptions)
    {
        filterOptions.Timeframe = returnedFilterOptions.Timeframe;
        await LoadMetrics();
    }
}
