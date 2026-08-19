namespace OrcaHello.Web.UI.Pages.Dashboard
{
    [ExcludeFromCodeCoverage]
    public partial class Metrics : ComponentManager
    {
        [Inject]
        public IDashboardViewService ViewService { get; set; } = null!;

        [Inject]
        public AppSettings AppSettings { get; set; } = null!;

        // Timeframe
        protected List<DropdownOption> TimeframeDropdownOptions = new()
        {
            new(Timeframe.ThirtyMinutes, "Last 30 Minutes"),
            new(Timeframe.TwentyFourHours, "Last 24 hours"),
            new(Timeframe.SevenDays, "Last Week"),
            new(Timeframe.ThirtyDays, "Last Month"),
            new(Timeframe.ThreeMonths, "Last 3 Months"),
            new(Timeframe.SixMonths, "Last 6 Months"),
            new(Timeframe.OneYear, "Last Year"),
            new(Timeframe.All, "All"),
            new(Timeframe.Range, "Select Date Range")
        };

        protected Timeframe SelectedTimeframe = Timeframe.ThirtyDays;

        // Datetime Range

        protected DateTime? SelectedStartDateTime = null;
        protected DateTime? SelectedEndDateTime = null;

        protected CommentStateView PositiveCommentsState = new(); // Positive Comments
        protected CommentStateView NegativeAndUnknownCommentsState = new(); // Negative and Unknown Comments
        protected TagStateView TagsState = new(); // Tags
        protected MetricsStateView MetricsState = new(); // Metrics
        protected string PlaybackId = string.Empty; // Currently Played SpectrographID

        #region lifecycle events

        protected override async Task OnInitializedAsync()
        {
            await SetNewDates();
        }

        #endregion

        #region button actions

        protected void OnTimeframeChanged()
        {
            if (SelectedTimeframe != Timeframe.Range)
            {
                SelectedStartDateTime = null;
                SelectedEndDateTime = null;
            }
            else if (SelectedStartDateTime == null || SelectedEndDateTime == null)
            {
                SelectedStartDateTime = DateTime.UtcNow.AddDays(-1);
                SelectedEndDateTime = DateTime.UtcNow;
            }
        }

        protected async Task OnApplyFilterClicked()
        {
            //await JSRuntime.InvokeVoidAsync("clearAllHowls");
            await SetNewDates();
        }

        protected async Task SetNewDates()
        {
            // This sets the initial value to Range (if populated) or All (if not)
            var fromDate = SelectedStartDateTime.HasValue ? SelectedStartDateTime.Value : AppSettings.EpochDate;
            var toDate = SelectedEndDateTime.HasValue ? SelectedEndDateTime.Value : DateTime.UtcNow;

            if (SelectedTimeframe != Timeframe.All && SelectedTimeframe != Timeframe.Range)
            {
                fromDate = toDate.Adjust(SelectedTimeframe);
            }

            // Reset and close all accordions
            MetricsState.Reset(fromDate, toDate);
            TagsState.Reset(fromDate, toDate);
            PositiveCommentsState.Reset(fromDate, toDate);
            NegativeAndUnknownCommentsState.Reset(fromDate, toDate);

            await InvokeAsync(StateHasChanged);
        }

        protected async Task OnToggleOpen()
        {
            //await JSRuntime.InvokeVoidAsync("clearAllHowls");

            TagsState.Close();
            PositiveCommentsState.Close();
            NegativeAndUnknownCommentsState.Close();

            await InvokeAsync(StateHasChanged);
        }

        #endregion
    }
}