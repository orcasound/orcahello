namespace AIForOrcas.Client.Web.Components;

public partial class TopBarComponent
{
    [Inject]
    IJSRuntime JSRuntime { get; set; }

    [Inject]
    IAccountService AccountService { get; set; }

    [Parameter]
    public string CurrentUrl { get; set; }

    private string DisplayName { get; set; }

    private string DisplayDate { get; set; }

    private string ShortDisplayDate { get; set; }

    public CancellationTokenSource CancellationTokenSource { get; set; }

    [Parameter]
    public EventCallback ToggleThemeCallback { get; set; }

    private string theme = "Dark";

    private void SetDateTime()
    {
        var now = DateTime.UtcNow;
        DisplayDate = DateHelper.UTCToPDT(now);
        ShortDisplayDate = DateHelper.UTCToPDT(now, true);
    }

    protected override async Task OnInitializedAsync()
    {
        DisplayName = await AccountService.GetDisplayname();
        SetDateTime();
        CancellationTokenSource = new CancellationTokenSource();
        await RealTimeUpdate(CancellationTokenSource.Token);
    }

    private async Task ToggleTheme()
    {
        theme = (theme == "Dark") ? "Light" : "Dark";
        await ToggleThemeCallback.InvokeAsync(null);
    }

    private async Task ToggleSidebar()
    {
        await JSRuntime.InvokeVoidAsync("ToggleSideBar");
    }

    private async Task Login()
    {
        await AccountService.Login();
    }

    public async Task RealTimeUpdate(CancellationToken cancellationToken)
    {
        while (!cancellationToken.IsCancellationRequested)
        {
            await Task.Delay(1000, cancellationToken);
            if (!cancellationToken.IsCancellationRequested)
            {
                SetDateTime();
                await InvokeAsync(() => this.StateHasChanged());
            }
        }
    }
}
