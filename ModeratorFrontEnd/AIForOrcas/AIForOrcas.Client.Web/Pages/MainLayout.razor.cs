namespace AIForOrcas.Client.Web.Pages;

public partial class MainLayout
{
    string darkTheme = string.Empty;

    [Inject]
    NavigationManager Nav { get; set; }

    private string CurrentUrl;

    public void ActOnToggleThemeCallback()
    {
        darkTheme = string.IsNullOrWhiteSpace(darkTheme) ? "css/sb-admin-2-dark.css" : "";
        StateHasChanged();
    }

    protected override void OnAfterRender(bool firstRender)
    {
        CurrentUrl = Nav.Uri;
        StateHasChanged();
    }
}
