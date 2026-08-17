using System;

namespace OrcaHello.Web.UI.Pages.Dashboard.Components
{
    [ExcludeFromCodeCoverage]
    public partial class DetectionMetricsComponent
    {
        [Inject]
        public IDashboardViewService ViewService { get; set; } = null!;

        [Parameter]
        public MetricsStateView StateView { get; set; } = null!;

        [Parameter]
        public string Moderator { get; set; } = null!;

        // Validation message for displaying error information.
        protected string ValidationMessage = null!;

        #region lifecycle events

        protected override async Task OnParametersSetAsync()
        {
            ValidationMessage = null!;

            await LoadMetricsAsync();
        }

        #endregion

        #region data loaders

        protected async Task LoadMetricsAsync()
        {
            StateView.IsLoading = true;

            MetricsByDateRequest request = new()
            {
                FromDate = StateView.FromDate,
                ToDate = StateView.ToDate
            };

            try
            {
                var response = Moderator != null
                    ? await ViewService.RetrieveFilteredMetricsForModeratorAsync(Moderator, request)
                    : await ViewService.RetrieveFilteredMetricsAsync(request);

                StateView.MetricsItemViews = response.MetricsItemViews;
                StateView.FillColors = response.MetricsItemViews.Select(x => x.Color).ToList();
            }
            catch (Exception exception)
            {
                // Handle data entry validation errors
                if (exception is DashboardViewValidationException ||
                    exception is DashboardViewDependencyValidationException)
                    ValidationMessage = ValidatorUtilities.GetInnerMessage(exception);
                else
                    // Report any other errors as unknown
                    LogAndReportUnknownException(exception);
            }
            finally
            {
                StateView.IsLoading = false;
            }
        }

        #endregion
    }
}
