namespace OrcaHello.Web.UI.Pages.Tags.Components
{
    /// <summary>
    /// Code-behind for the ConfirmDeleteComponent.razor page, providing functionality for confirming tag deletion.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public partial class ConfirmDeleteComponent
    {
        /// <summary>
        /// Injected service for interacting with tag-related data.
        /// </summary>
        [Inject]
        public ITagViewService ViewService { get; set; } = null!;

        /// <summary>
        /// Represents the TagItemView to be deleted.
        /// </summary>
        [Parameter]
        public TagItemView Item { get; set; } = null!;

        /// <summary>
        /// Event callback to notify the parent component when the dialog is closed.
        /// </summary>
        [Parameter]
        public EventCallback<bool> OnCloseClicked { get; set; }

        /// <summary>
        /// Handles the confirmation of tag deletion.
        /// </summary>
        protected async Task OnConfirmClicked()
        {
            try
            {
                // Attempt to delete the tag using the ViewService
                var response = await ViewService.DeleteTagAsync(Item);

                // Check if the tag deletion was successful and report accordingly
                if (response.MatchingTags == response.ProcessedTags)
                {
                    ReportSuccess("Success", $"'{Item.Tag}' was successfully deleted from all detections (and will disappear from this list).");
                }
                else
                {
                    ReportError("Failure", $"'{Item.Tag}' was not deleted from one or more detections.");
                }

                // Invoke the OnCloseClicked event with a parameter indicating success
                await OnCloseClicked.InvokeAsync(true);
            }
            catch (Exception exception)
            {
                LogAndReportUnknownException(exception);
            }
        }

        /// <summary>
        /// Handles the cancellation of tag deletion, notifying the parent component that the dialog is closed.
        /// </summary>
        protected async Task OnCancelClicked()
        {
            // Invoke the OnCloseClicked event with a parameter indicating cancellation
            await OnCloseClicked.InvokeAsync(false);
        }
    }
}