using System;

namespace OrcaHello.Web.UI.Pages.Dashboard.Components
{
    [ExcludeFromCodeCoverage]
    public partial class NegativeAndUnknownCommentsComponent
    {
        [Inject]
        public IDashboardViewService ViewService { get; set; } = null!;

        [Parameter]
        public CommentStateView StateView { get; set; } = null!;

        [Parameter]
        public EventCallback OnToggleOpen { get; set; }

        [Parameter]
        public string Moderator { get; set; } = null!;

        [Parameter]
        public string PlaybackId { get; set; } = string.Empty; // Currently Played SpectrographID

        [Parameter]
        public EventCallback<string> PlaybackIdChanged { get; set; }

        #region component buttons

        protected async Task ToggleNegativeAndUnknownComments()
        {
            // Getting ready to expand this component, so make sure all the other ones are closed

            if (!StateView.IsExpanded)
            {
                await OnToggleOpen.InvokeAsync();
            }

            StateView.Toggle();

            if (!StateView.Items.Any())
            {
                await LoadNegativeAndUnknownCommentsAsync();
            }

        }

        protected async Task OnLoadMoreNegativeAndUnknownCommentsClicked()
        {
            StateView.Page++;
            await LoadNegativeAndUnknownCommentsAsync();
        }

        #endregion

        #region data loaders

        protected async Task LoadNegativeAndUnknownCommentsAsync()
        {
            StateView.IsLoading = true;

            PaginatedCommentsByDateRequest request = new()
            {
                Page = StateView.Page,
                PageSize = StateView.PageSize,
                FromDate = StateView.FromDate,
                ToDate = StateView.ToDate,
            };

            try
            {
                var response = Moderator != null
                    ? await ViewService.RetrieveFilteredNegativeAndUnknownCommentsForModeratorAsync(Moderator, request)
                    : await ViewService.RetrieveFilteredNegativeAndUnknownCommentsAsync(request);

                // Update the Data property
                if (response.CommentItemViews.Any())
                {
                    StateView.Items.AddRange(response.CommentItemViews);
                }

                // Update the count
                StateView.Count = response.Count;
            }
            catch (Exception exception)
            {
                LogAndReportUnknownException(exception);
            }

            StateView.IsLoading = false;
        }

        #endregion
    }
}
