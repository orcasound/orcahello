namespace AIForOrcas.Client.Web.Components;

public partial class ReviewedFilterComponent
{
    [Parameter]
    public ReviewedFilterOptionsDTO FilterOptions { get; set; } = new ReviewedFilterOptionsDTO();

    [Parameter]
    public EventCallback<ReviewedFilterOptionsDTO> ApplyFilterCallback { get; set; }

    [Inject]
    public AppSettings AppSettings { get; set; }

    private List<KeyValuePair<string, string>> Locations = new List<KeyValuePair<string, string>>();

    protected override void OnInitialized()
    {
        Locations = HydrophoneLocations.Locations
            .Select(location => new KeyValuePair<string, string>(location, HydrophoneLocations.GetIdByLocation(location)))
            .Where(location => location.Value != null)
            .ToList();

        FilterOptions.Location = "all";
        FilterOptions.HydrophoneId ??= "all";
    }

    private async Task ApplyFilter()
    {
        FilterOptions.Location = "all";
        FilterOptions.HydrophoneId ??= "all";
        await ApplyFilterCallback.InvokeAsync(FilterOptions);
    }
}
