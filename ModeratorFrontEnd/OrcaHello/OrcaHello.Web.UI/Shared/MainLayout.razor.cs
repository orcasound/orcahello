namespace OrcaHello.Web.UI.Shared
{
    [ExcludeFromCodeCoverage]
    public partial class MainLayout
    {
        protected bool sidebarExpanded = true;
        protected DateTime RightNow = DateTime.UtcNow;
        protected bool isSummarySelected = false;

        protected override void OnAfterRender(bool firstRender)
        {
            var rel = NavManager.ToBaseRelativePath(NavManager.Uri)?.ToLower();
            isSummarySelected = string.IsNullOrWhiteSpace(rel) ||
                rel == "index" ||
                rel.StartsWith("summary");
            StateHasChanged();
        }
    }
}
