namespace AIForOrcas.Client.Web.Components;

public partial class ReviewedFilterComponent
{
    [Parameter]
    public ReviewedFilterOptionsDTO FilterOptions { get; set; } = new ReviewedFilterOptionsDTO();

    [Parameter]
    public EventCallback<ReviewedFilterOptionsDTO> ApplyFilterCallback { get; set; }

    [Inject]
    public AppSettings AppSettings { get; set; }

    private List<string> AllLocations = new List<string>();

    protected override void OnInitialized()
    {
        AllLocations = HydrophoneLocations.Locations.ToList();
    }

    private async Task ApplyFilter()
    {
        await ApplyFilterCallback.InvokeAsync(FilterOptions);
    }
}
