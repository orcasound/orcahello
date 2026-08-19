namespace OrcaHello.Web.UI.Pages.Tags
{
    /// <summary>
    /// Code-behind for the TagSearch.razor page, providing functionality for searching and filtering detection data by tags.
    /// </summary>
    [ExcludeFromCodeCoverage]
    public partial class TagSearch : ComponentManager
    {
        // Injected service for interacting with tag-related data.
        [Inject]
        public ITagViewService ViewService { get; set; } = null!;

        // Injected settings for the application.
        [Inject]
        public AppSettings AppSettings { get; set; } = null!;

        // Dropdown options for selecting the timeframe.
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

        // Selected timeframe for filtering detection data.
        protected Timeframe SelectedTimeframe = Timeframe.ThirtyDays;

        // Selected start and end date and time for custom date range filtering.
        protected DateTime? SelectedStartDateTime = null;
        protected DateTime? SelectedEndDateTime = null;

        // Collection of available tags for filtering.
        public List<string> AvailableTags { get; set; } = new();

        // Tags selected from AvailableTags for filtering detection data.
        protected List<string> SelectedTags = new();

        // Dropdown options for selecting the logical operator between tags.
        protected List<DropdownOption> TagLogicDropdownOptions = new()
        {
            new(LogicalOperator.And, "AND"),
            new(LogicalOperator.Or, "OR")
        };

        // Selected logical operator for filtering detection data.
        protected LogicalOperator SelectedTagLogic = LogicalOperator.Or;

        // Validation message for displaying error information.
        protected string ValidationMessage = null!;

        // Loading indicator for asynchronous operations.
        protected bool IsLoading = false;

        // Current page number for paginated detection data.
        protected int Page = 1;

        // Number of items per page for paginated detection data.
        protected int PageSize = 10;

        // Total count of matching detections.
        protected int TotalCount = 0;

        // Collection of detection items loaded so far.
        protected List<DetectionItemView> DetectionItemViews = null!;

        // Currently spectrograph ID for playback management.
        protected string PlaybackId { get; set; } = string.Empty;

        // Indicates whether the detection list is empty after filtering.
        protected bool IsEmptyList => !IsLoading && string.IsNullOrWhiteSpace(ValidationMessage)
            && DetectionItemViews != null && !DetectionItemViews.Any();

        // Indicates whether the detection list is populated after filtering.
        protected bool IsPopulatedList => !IsLoading && string.IsNullOrWhiteSpace(ValidationMessage)
            && DetectionItemViews != null && DetectionItemViews.Any();

        #region lifecycle events

        /// <summary>
        /// Initializes the component asynchronously.
        /// </summary>
        protected override async Task OnInitializedAsync()
        {
            await LoadTags();
            OnTimeframeChanged();
        }

        #endregion

        #region button actions

        /// <summary>
        /// Handles the event when the timeframe selection is changed.
        /// </summary>
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

        /// <summary>
        /// Applies the selected filters and loads paginated detection data.
        /// </summary>
        protected async Task OnApplyFilterClicked()
        {
            DetectionItemViews = new();
            Page = 1;
            TotalCount = 0;
            ValidationMessage = null!;

            //await JSRuntime.InvokeVoidAsync("clearAllHowls");

            await LoadPaginatedDetectionsAsync();
        }

        /// <summary>
        /// Loads more detection data for pagination.
        /// </summary>
        protected async Task OnLoadMoreDetectionsClicked()
        {
            Page++;

            await LoadPaginatedDetectionsAsync();
        }

        #endregion

        #region data loaders

        /// <summary>
        /// Loads available tags asynchronously.
        /// </summary>
        private async Task LoadTags()
        {
            try
            {
                var result = await ViewService.RetrieveAllTagViewsAsync();

                AvailableTags = result.Select(x => x.Tag).ToList();
            }
            catch (Exception exception)
            {
                LogAndReportUnknownException(exception);
            }
        }

        /// <summary>
        /// Loads paginated detection data asynchronously based on filter criteria.
        /// </summary>
        protected async Task LoadPaginatedDetectionsAsync()
        {
            IsLoading = true;

            // This sets the initial value to Range (if populated) or All (if not)
            var fromDate = SelectedStartDateTime.HasValue ? SelectedStartDateTime.Value : AppSettings.EpochDate;
            var toDate = SelectedEndDateTime.HasValue ? SelectedEndDateTime.Value : DateTime.UtcNow;

            // Now adjust the fromDate if appropriate
            if (SelectedTimeframe != Timeframe.All && SelectedTimeframe != Timeframe.Range)
            {
                fromDate = toDate.Adjust(SelectedTimeframe);
            }

            PaginatedDetectionsByTagsAndDateRequest request = new()
            {
                Page = Page,
                PageSize = PageSize,
                FromDate = fromDate,
                ToDate = toDate,
                Tags = SelectedTags,
                Logic = SelectedTagLogic
            };

            try
            {
                var response = await ViewService.RetrieveDetectionsByTagsAsync(request);

                if (response.DetectionItemViews.Any())
                {
                    DetectionItemViews.AddRange(response.DetectionItemViews);
                }

                TotalCount = response.Count;
            }
            catch (Exception exception)
            {
                // Handle data entry validation errors
                if (exception is TagViewValidationException ||
                    exception is TagViewDependencyValidationException)
                {
                    ValidationMessage = ValidatorUtilities.GetInnerMessage(exception);
                }
                else
                {
                    // Report any other errors as unknown
                    LogAndReportUnknownException(exception);
                }
            }

            IsLoading = false;
        }

        #endregion
    }
}
