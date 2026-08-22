namespace AIForOrcas.Client.Web.Components;

public partial class SideBarComponent
{
    [Inject]
    IJSRuntime JSRuntime { get; set; }

    protected override async Task OnAfterRenderAsync(bool firstRender)
    {
        if (firstRender)
        {
            await JSRuntime.InvokeVoidAsync("CollapseSideBarOnSmallScreens");
        }
    }

    private async Task ToggleDisplay()
    {
        await JSRuntime.InvokeVoidAsync("ToggleSideBar");
    }
}
