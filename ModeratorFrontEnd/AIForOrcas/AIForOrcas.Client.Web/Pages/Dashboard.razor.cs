namespace AIForOrcas.Client.Web.Pages;

public partial class Dashboard
{
    [Inject]
    IMetricsService Service { get; set; }

    [Inject]
    IJSRuntime JSRuntime { get; set; }

    private Metrics metrics = null;

    private MetricsFilterDTO filterOptions =
        new MetricsFilterDTO() { Timeframe = "1m" };

    private string messageStyle = "d-none";
    private string message = string.Empty;
    private string displayStyle = "d-none";

    protected override async Task OnInitializedAsync()
    {
        await LoadMetrics();
    }

    private async Task LoadMetrics()
    {
        displayStyle = "d-none";
        messageStyle = "";
        message = "Loading metrics...";

        metrics = await Service.GetSiteMetricsAsync(filterOptions);

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
        filterOptions = returnedFilterOptions;
        await LoadMetrics();
    }

}
