namespace OrcaHello.Web.UI.Pages.Components
{
    [ExcludeFromCodeCoverage]
    public partial class InlinePlayerComponent
    {
        [Parameter]
        public string AudioUri { get; set; } = string.Empty;

        [Parameter]
        public string SpectrogramUri { get; set; } = string.Empty;

        [Parameter]
        public string Id { get; set; } = string.Empty;

        protected override async Task OnAfterRenderAsync(bool firstRender)
        {
            if (firstRender)
            {
                await JSRuntime.InvokeVoidAsync("addAudioEventStopper", Id);
            }
        }
    }
}
