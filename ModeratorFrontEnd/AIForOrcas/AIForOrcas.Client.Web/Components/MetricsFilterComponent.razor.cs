namespace AIForOrcas.Client.Web.Components;

public partial class MetricsFilterComponent
{
    [Parameter]
    public MetricsFilterDTO FilterOptions { get; set; } = new MetricsFilterDTO();

    [Parameter]
    public EventCallback<MetricsFilterDTO> ApplyFilterCallback { get; set; }

    private async Task ApplyFilter()
    {
        await ApplyFilterCallback.InvokeAsync(FilterOptions);
    }
}
