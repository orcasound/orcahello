using System;

namespace OrcaHello.Web.UI.Pages.Dashboard.Components
{
    [ExcludeFromCodeCoverage]
    public partial class TagsComponent
    {
        [Inject]
        public IDashboardViewService ViewService { get; set; } = null!;

        [Parameter]
        public TagStateView StateView { get; set; } = null!;

        [Parameter]
        public EventCallback OnToggleOpen { get; set; }

        [Parameter]
        public string Moderator { get; set; } = null!;

        [Parameter]
        public string PlaybackId { get; set; } = string.Empty; // Currently Played SpectrographID

        [Parameter]
        public EventCallback<string> PlaybackIdChanged { get; set; }

        protected TagDetectionStateView TagDetectionsState = new();

        #region component buttons

        protected async Task ToggleTags()
        {
            // Getting ready to expand this component, so make sure all the other ones are closed

            if (!StateView.IsExpanded)
            {
                await OnToggleOpen.InvokeAsync();
            }

            StateView.Toggle();

            if (!StateView.Items.Any())
            {
                await LoadTagsAsync();
            }
        }

        protected async Task OnTagExpanded(object value)
        {
            int selectedTagIndex = (int)value;

            TagDetectionsState.SelectedTagReset(StateView.Items[selectedTagIndex]);

            await LoadTagDetectionsAsync();
        }

        protected async Task OnLoadMoreTagDetectionsClicked()
        {
            TagDetectionsState.Page++;

            await LoadTagDetectionsAsync();
        }

        #endregion

        #region data loaders

        protected async Task LoadTagsAsync()
        {
            StateView.IsLoading = true;

            TagsByDateRequest request = new()
            {
                FromDate = StateView.FromDate,
                ToDate = StateView.ToDate,
            };

            try
            {
                StateView.Items = Moderator != null
                    ? await ViewService.RetrieveFilteredTagsForModeratorAsync(Moderator, request)
                    : await ViewService.RetrieveFilteredTagsAsync(request);
                StateView.Count = StateView.Items.Count;
            }
            catch (Exception exception)
            {
                LogAndReportUnknownException(exception);
            }

            StateView.IsLoading = false;

            await InvokeAsync(StateHasChanged);
        }

        protected async Task LoadTagDetectionsAsync()
        {
            TagDetectionsState.IsLoading = true;

            PaginatedDetectionsByTagAndDateRequest request = new()
            {
                Page = TagDetectionsState.Page,
                PageSize = TagDetectionsState.PageSize,
                FromDate = StateView.FromDate,
                ToDate = StateView.ToDate,
                Tag = TagDetectionsState.Tag
            };

            try
            {
                var result = Moderator != null
                    ? await ViewService.RetrieveFilteredDetectionsForTagAndModeratorAsync(Moderator, request)
                    : await ViewService.RetrieveFilteredDetectionsForTagsAsync(request);

                // Update the Data property
                if (result.DetectionItemViews.Any())
                {
                    TagDetectionsState.Items.AddRange(result.DetectionItemViews);
                }

                // Update the count
                TagDetectionsState.Count = result.Count;
            }
            catch (Exception ex)
            {
                ReportError("Trouble loading Tag Detections data", ex.Message);
            }

            TagDetectionsState.IsLoading = false;
        }

        #endregion
    }
}
