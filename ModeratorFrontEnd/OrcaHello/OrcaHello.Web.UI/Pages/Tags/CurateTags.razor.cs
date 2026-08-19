namespace OrcaHello.Web.UI.Pages.Tags
{
    /// <summary>
    /// Code-behind for the CurateTags.razor page, a Razor component for curating tags.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public partial class CurateTags : ComponentManager
    {
        // Inject the ITagViewService dependency
        [Inject]
        public ITagViewService ViewService { get; set; } = null!;

        // Collection to hold TagItemView instances
        protected List<TagItemView> TagItemViews = null!;

        // Flag indicating whether data is currently being loaded
        protected bool IsLoading = false;

        // RadzenDataGrid instance for displaying TagItemView data
        RadzenDataGrid<TagItemView> DataGrid = null!;

        #region button actions

        /// <summary>
        /// Opens a dialog for replacing a tag.
        /// </summary>
        public async Task ReplaceTagClicked(TagItemView item)
        {
            await DialogService.OpenAsync<ReplaceTagComponent>(
               "Replace Tag",
               new Dictionary<string, object>() {
                    { "Item", item },
                     { "OnCloseClicked", EventCallback.Factory.Create<bool>(this, HandleDialogClosed) }
               },
               new DialogOptions() { Width = "450px", Height = "270px", Resizable = false, Draggable = true }
            );
        }

        /// <summary>
        /// Opens a dialog for confirming tag deletion.
        /// </summary>
        public async Task DeleteTagClicked(TagItemView item)
        {
            await DialogService.OpenAsync<ConfirmDeleteComponent>(
               "Delete Tag?",
               new Dictionary<string, object>() {
                    { "Item", item },
                     { "OnCloseClicked", EventCallback.Factory.Create<bool>(this, HandleDialogClosed) }
               },
               new DialogOptions() { Width = "450px", Height = "200px", Resizable = false, Draggable = true }
            );
        }

        /// <summary>
        /// Handles the closure of the dialog, optionally triggering a data reload.
        /// </summary>
        public async Task HandleDialogClosed(bool reload)
        {
            DialogService.Close();

            if (reload)
            {
                await ReloadData();
            }
        }

        #endregion

        #region data loaders

        /// <summary>
        /// Reloads data in the DataGrid.
        /// </summary>
        async Task ReloadData()
        {
            // Reset DataGrid
            DataGrid.Reset();

            // Reload data in the DataGrid
            await DataGrid.Reload();
        }

        /// <summary>
        /// Loads data asynchronously, updating the UI accordingly.
        /// </summary>
        private async Task LoadData(LoadDataArgs args)
        {
            try
            {
                // Set the loading flag to true and update UI
                IsLoading = true;
                await InvokeAsync(StateHasChanged);

                // Retrieve all TagItemViews from the ViewService
                var result = await ViewService.RetrieveAllTagViewsAsync();

                // Update the TagItemViews collection with the retrieved data
                TagItemViews = result;
            }
            catch (Exception exception)
            {
                LogAndReportUnknownException(exception);
            }
            finally
            {
                // Set the loading flag to false and update UI
                IsLoading = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        #endregion
    }
}
