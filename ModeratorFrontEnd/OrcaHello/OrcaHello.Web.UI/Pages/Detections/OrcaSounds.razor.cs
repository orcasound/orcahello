namespace OrcaHello.Web.UI.Pages.Detections
{
    [ExcludeFromCodeCoverage]
    public partial class OrcaSounds
    {
        [Inject]
        public AppSettings AppSettings { get; set; } = null!;

        // View Mode
        protected ViewMode SelectedViewMode = ViewMode.TileView;

        // Detection State
        protected List<DropdownOption> DetectionStateDropdownOptions = new() {
            new(DetectionState.Unreviewed, "Unreviewed Calls"),
            new(DetectionState.Positive, "Verified Calls"),
            new(DetectionState.Negative, "Rejected Calls"),
            new(DetectionState.Unknown, "Unclear Calls" )};

        protected DetectionState SelectedDetectionState;

        // Sort By
        protected List<DropdownOption> SortByDropdownOptions = new()
        {
            new(SortBy.Confidence, "Confidence"),
            new(SortBy.Timestamp, "Timestamp")
        };

        protected SortBy SelectedSortBy;

        // Sort Order
        protected List<DropdownOption> SortOrderDropdownOptions = new()
        {
            new(Models.SortOrder.Asc, "Ascending"),
            new(Models.SortOrder.Desc, "Descending")
        };

        protected Models.SortOrder SelectedSortOrder;

        // Timeframe
        protected List<DropdownOption> TimeframeDropdownOptions = new()
        {
            new(Timeframe.ThirtyMinutes, "Last 30 Minutes"),
            new(Timeframe.ThreeHours, "Last 3 Hours"),
            new(Timeframe.SixHours, "Last 6 hours"),
            new(Timeframe.TwentyFourHours, "Last 24 hours"),
            new(Timeframe.SevenDays, "Last Week"),
            new(Timeframe.ThirtyDays, "Last Month"),
            new(Timeframe.All, "All"),
            new(Timeframe.Range, "Select Date Range")
        };

        protected Timeframe SelectedTimeframe;

        // Datetime Range

        protected DateTime? SelectedStartDateTime = null;
        protected DateTime? SelectedEndDateTime = null;

        // Locations
        List<DropdownOption> LocationDropdownOptions = new();
        protected int SelectedLocation;

        // Maximum Records
        List<DropdownOption> MaxRecordsDropdownOptions = new()
        {
            new(10, "10"),
            new(25, "25"),
            new(50, "50"),
            new(100, "100"),
            new(250, "250"),
            new(500, "500"),
            new(1000, "1000")
        };

        protected int SelectedMaxRecords;

        protected Filters PassedFilters = new();

        // Review button
        protected bool IsReviewButtonVisible = false;

        // Reference to GridViewComponent
        protected GridViewComponent GridView = null!;

        // Reference to TileViewComponent
        protected TileViewComponent TileView = null!;

        // Pill Count
        protected int PillCount = 0;

        #region lifecycle events

        protected override void OnInitialized()
        {
            int index = 0;
            foreach (var location in AppSettings.HydrophoneLocationNames)
            {
                LocationDropdownOptions.Add(new(index++, location));
            }
            LocationDropdownOptions.Add(new(index, "All"));
            SelectedLocation = index;

            SelectedDetectionState = DetectionState.Unreviewed;

            SetFilterDefaults();

            // Initially sync the passed values with the selected values
            OnApplyFilterClicked();
        }

        #endregion

        #region helpers

        protected void SetFilterDefaults()
        {
            SelectedSortBy = SortBy.Timestamp;
            SelectedSortOrder = Models.SortOrder.Asc;
            SelectedMaxRecords = 10;
            SelectedStartDateTime = null;
            SelectedEndDateTime = null;
            SelectedLocation = LocationDropdownOptions.Count - 1;

            switch (SelectedDetectionState)
            {
                case DetectionState.Unreviewed:
                    SelectedTimeframe = Timeframe.SixHours;
                    break;
                case DetectionState.Positive:
                case DetectionState.Negative:
                case DetectionState.Unknown:
                    SelectedTimeframe = Timeframe.TwentyFourHours;
                    break;
            }
        }

        #endregion

        #region button actions

        // Needed in order to select the correct default timeframe

        protected void OnDetectionStateChanged()
        {
            SetFilterDefaults();
            OnApplyFilterClicked();
        }

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

        protected void OnApplyFilterClicked()
        {
            PassedFilters = new()
            {
                DetectionState = SelectedDetectionState,
                SortBy = SelectedSortBy,
                SortOrder = SelectedSortOrder,
                Timeframe = SelectedTimeframe,
                StartDateTime = SelectedStartDateTime,
                EndDateTime = SelectedEndDateTime,
                Location = LocationDropdownOptions[SelectedLocation].Text,
                MaxRecords = SelectedMaxRecords
            };
        }

        protected async Task OnGridViewClicked()
        {
            await JSRuntime.InvokeVoidAsync("stopAllAudio");

            SelectedViewMode = ViewMode.GridView;
        }

        protected async Task OnTileViewClicked()
        {
            await JSRuntime.InvokeVoidAsync("stopAllAudio");

            IsReviewButtonVisible = false;
            SelectedViewMode = ViewMode.TileView;
        }

        protected void HideReviewButton()
        {
            IsReviewButtonVisible = false;
        }

        protected void ShowReviewButton()
        {
            IsReviewButtonVisible = true;
        }

        protected async Task OnReviewClicked()
        {
            await GridView.OnReviewClicked();
        }

        #endregion
    }
}