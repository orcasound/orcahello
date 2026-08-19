using System.Security.Policy;

namespace OrcaHello.Web.UI.Pages.Detections.Components
{
    [ExcludeFromCodeCoverage]
    public partial class TileViewComponent
    {
        [Inject]
        public IDetectionViewService ViewService { get; set; } = null!;

        [Inject]
        public IAccountService AccountService { get; set; } = null!;

        [Parameter]
        public Filters Filters { get; set; } = null!;

        [Parameter]
        public int PillCount { get; set; }

        [Parameter]
        public EventCallback<int> PillCountChanged { get; set; }

        RadzenDataList<DetectionItemView> DetectionDataList = null!; // Reference to the list component

        protected IEnumerable<DetectionItemView> DetectionItemViews = null!; // List of items currently in the list

        protected bool IsLoading = false; // Flag indicating data is being loaded into the list

        protected int TotalDetectionCount = 0; // The total number of records for the applied filter

        protected Filters CurrentlySetFilters = new(); // The currently "applied" filters; so they don't get updated until Apply is clicked

        protected string Moderator = string.Empty; // The name of the current moderator, if logged in

        protected List<string> AvailableTags = new(); // All unique tags in the system for picklists

        // Validation message for displaying error information.
        protected string ValidationMessage = null!;

        #region lifecycle events

        protected override async Task OnInitializedAsync()
        {
            Moderator = await AccountService.GetUserName();
            AvailableTags = await ViewService.RetrieveAllTagsAsync();
        }

        protected override async Task OnParametersSetAsync()
        {
            ValidationMessage = null!;

            if (CurrentlySetFilters != Filters)
            {
                CurrentlySetFilters = Filters;

                if (DetectionDataList?.Data != null)
                {
                    await DetectionDataList.Reload();
                }
            }
        }

        #endregion

        #region data loaders

        async Task StopAllAudio()
        {
            await JSRuntime.InvokeVoidAsync("stopAllAudio");
        }

        async Task ReloadData()
        {
            DetectionItemViews = null!;
            await DetectionDataList.Reload();
        }

        private async Task LoadData(LoadDataArgs args)
        {
            await StopAllAudio();

            IsLoading = true;
            await InvokeAsync(StateHasChanged);

            // This sets the initial value to Range (if populated) or All (if not)
            var fromDate = Filters.StartDateTime.HasValue ? Filters.StartDateTime.Value : new DateTime(1970, 1, 1);
            var toDate = Filters.EndDateTime.HasValue ? Filters.EndDateTime.Value : DateTime.UtcNow;

            if (Filters.Timeframe != Timeframe.All && Filters.Timeframe != Timeframe.Range)
            {
                fromDate = toDate.Adjust(Filters.Timeframe);
            }

            int skip = args.Skip.HasValue ? args.Skip.Value : 1;
            int top = args.Top.HasValue ? args.Top.Value : 10;

            PaginatedDetectionsByStateRequest filterAndPagination = new()
            {
                Page = (skip / top) + 1,
                PageSize = Filters.MaxRecords,
                State = Filters.DetectionState.ToString(),
                SortBy = Filters.SortBy.ToString().ToLower(),
                IsDescending = Filters.SortOrder == Models.SortOrder.Desc,
                FromDate = fromDate,
                ToDate = toDate,
                Location = Filters.Location == "All" ? string.Empty : Filters.Location
            };

            try
            {
                var result = await ViewService.
                    RetrieveFilteredAndPaginatedDetectionItemViewsAsync(filterAndPagination);

                // Update the Data property
                DetectionItemViews = result.DetectionItemViews;

                // Update the count
                TotalDetectionCount = result.Count;

                // Update the PillCount
                PillCount = result.Count;
                await PillCountChanged.InvokeAsync(PillCount);
            }
            catch (Exception exception)
            {
                // Handle data entry validation errors
                if (exception is DetectionViewValidationException ||
                    exception is DetectionViewDependencyValidationException)
                {
                    ValidationMessage = ValidatorUtilities.GetInnerMessage(exception);
                }
                else
                {
                    // Report any other errors as unknown
                    LogAndReportUnknownException(exception);
                }
            }
            finally
            {
                IsLoading = false;
                await InvokeAsync(StateHasChanged);
            }
        }

        #endregion
    }
}
