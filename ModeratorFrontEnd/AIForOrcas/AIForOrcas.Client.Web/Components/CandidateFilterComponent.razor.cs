namespace AIForOrcas.Client.Web.Components;

public partial class CandidateFilterComponent
{
    [Parameter]
    public CandidateFilterOptionsDTO FilterOptions { get; set; } = new CandidateFilterOptionsDTO();

    [Parameter]
    public EventCallback<CandidateFilterOptionsDTO> ApplyFilterCallback { get; set; }

    [Inject]
    public AppSettings AppSettings { get; set; }

    private List<string> AllLocations = new List<string>();

    // Local UI-only tracking of selected location.
    private string SelectedLocation { get; set; } = "all";

    protected override void OnInitialized()
    {
        AllLocations = HydrophoneLocations.Locations.ToList();
        // Don't initialize from FilterOptions.Location - keep it independent.
        SelectedLocation = "all";
    }

    private async Task ApplyFilter()
    {
        // The HydrophoneId (e.g., rpi_orcasound_lab) is constant whereas the Location
        // value can and has changed over time (e.g., Haro Strait vs Orcasound Lab).
        // The UI lets the user choose among labels that are Location values, but
        // we want to actually query by HydrophoneId.

        // Always set location to "all" so backend doesn't filter by location name.
        FilterOptions.Location = "all";

        if (SelectedLocation != "all")
        {
            var hydrophoneId = HydrophoneLocations.GetIdByLocation(SelectedLocation);
            if (hydrophoneId != null)
            {
                FilterOptions.HydrophoneId = hydrophoneId;
            }
            else
            {
                // Location not found in map, default to "all".
                FilterOptions.HydrophoneId = "all";
            }
        }
        else
        {
            FilterOptions.HydrophoneId = "all";
        }

        await ApplyFilterCallback.InvokeAsync(FilterOptions);
    }
}
