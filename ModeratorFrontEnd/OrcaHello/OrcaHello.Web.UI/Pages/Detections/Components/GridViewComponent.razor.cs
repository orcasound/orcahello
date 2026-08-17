using System.Linq.Expressions;

namespace OrcaHello.Web.UI.Pages.Detections.Components
{
    [ExcludeFromCodeCoverage]
    public partial class GridViewComponent
    {
        [Inject]
        public IDetectionViewService ViewService { get; set; } = null!;

        [Inject]
        public IAccountService AccountService { get; set; } = null!;

        [Parameter]
        public Filters Filters { get; set; } = null!;

        [Parameter]
        public EventCallback OnShowReviewButton { get; set; }

        [Parameter]
        public int PillCount { get; set; }

        [Parameter]
        public EventCallback<int> PillCountChanged { get; set; }

        RadzenDataGrid<DetectionItemView> DetectionDataGrid = null!; // Reference to the grid component

        protected bool IsLoading = false; // Flag indicating data is being loaded into the grid

        protected List<DetectionItemView> DetectionItemViews = null!; // List of items currently in the grid

        IList<DetectionItemView> SelectedDetectionItemViews = new List<DetectionItemView>(); // Which items have been selected in the grid

        protected Filters CurrentlySetFilters = new(); // The currently "applied" filters; so they don't get updated until Apply is clicked

        protected int TotalDetectionCount = 0; // The total number of records for the applied filter

        protected DetectionItemView ActiveItem = null!; // Holder for item being edited or played

        protected bool IsInlineEditing = false; // Flag indicating an inline edit is under way

        protected string Moderator = string.Empty; // The name of the current moderator, if logged in

        protected List<string> AvailableTags = new(); // All unique tags in the system for picklists

        // Validation message for displaying error information.
        protected string ValidationMessage = null!;

        #region lifecycle events

        protected override async Task OnInitializedAsync()
        {
            Moderator = await AccountService.GetUserName();
            AvailableTags = await ViewService.RetrieveAllTagsAsync();

            await OnShowReviewButton.InvokeAsync();
        }

        protected override async Task OnParametersSetAsync()
        {
            ValidationMessage = null!;

            if (CurrentlySetFilters != Filters)
            {
                CurrentlySetFilters = Filters;

                if (DetectionDataGrid?.Data != null)
                    await DetectionDataGrid.Reload();
            }
        }

        #endregion

        #region bulk review

        protected void OnAllItemsChanged(bool? value)
        {
            SelectedDetectionItemViews = value == true ? DetectionItemViews.ToList() : null!;
        }

        public async Task OnReviewClicked()
        {
            if (IsInlineEditing)
                ReportError("Inline Edit",
                    "You must complete your inline edit of data before you can perform a bulk review.");

            else if (SelectedDetectionItemViews == null || !SelectedDetectionItemViews.Any())
                ReportError("Select a Record",
                    "You must select one or more records to bulk review.");

            else
            {
                List<string> selectedIds = SelectedDetectionItemViews.Select(x => x.Id).ToList();

                await DialogService.OpenAsync<BulkReviewComponent>(
                   "Bulk Review of Candidates",
                   new Dictionary<string, object>() {
                       { "SelectedIds", selectedIds },
                       { "Moderator", Moderator },
                       { "AvailableTags", AvailableTags },
                       { "OnCloseClicked", EventCallback.Factory.Create<bool>(this, HandleBulkReviewClosed) }
                   },
                   new DialogOptions() { Width = "450px", Height = "525px", Resizable = false, Draggable = true }
                );
            }
        }

        public async Task HandleBulkReviewClosed(bool reload)
        {
            DialogService.Close();

            if (reload)
                await ReloadData();
        }

        #endregion

        #region inline editing events

        public async Task OnInlineEditRowClicked(DetectionItemView item)
        {
            IsInlineEditing = true;

            ActiveItem = new DetectionItemView
            {
                State = item.State,
                Comments = item.Comments,
                Tags = item.Tags
            };

            await DetectionDataGrid.EditRow(item);
        }

        public async Task OnSaveRowClicked(DetectionItemView item)
        {
            if (string.IsNullOrWhiteSpace(item.State) || item.State == DetectionState.Unreviewed.ToString())
                ReportError("Select Call State",
                    "A Call State (Yes, No, Don't Know) must be selected in order to save this record.");

            else
            {
                var response = await ViewService.ModerateDetectionsAsync(
                    new List<string> { item.Id },
                    item.State,
                    Moderator,
                    item.Comments,
                    item.Tags);

                IsInlineEditing = false;

                if (response != null)
                {
                    if (response.IdsSuccessfullyUpdated.Count > 0)
                        ReportSuccess("Success",
                            "Record was successfully updated (and may disappear from this list).");

                    else if (response.IdsNotFound.Count > 0)
                        ReportError("Not Found",
                            "Record not found in the database and could not be updated.");
                    else
                        ReportError("Failure",
                            "An unknow error occurred while updating the record.");
                }

                await ReloadData();
            }
        }

        public void OnCancelEditClicked(DetectionItemView item)
        {
            item.State = ActiveItem.State;
            item.Comments = ActiveItem.Comments;
            item.Tags = ActiveItem.Tags;

            ActiveItem = null!;
            IsInlineEditing = false;

            DetectionDataGrid.CancelEditRow(item);
        }

        protected void OnEnteredTagsChanged()
        {
            if (string.IsNullOrWhiteSpace(ActiveItem?.Tags)) return;

            var enteredTags = ActiveItem.Tags.Split(new char[] { ',', ';' }, StringSplitOptions.RemoveEmptyEntries)
                                              .Select(s => s.Trim())
                                              .Distinct();

            var newTags = enteredTags.Except(AvailableTags);
            AvailableTags.AddRange(newTags);
            AvailableTags.Sort();
        }

        #endregion

        #region data loaders

        async Task StopAllAudio()
        {
            await JSRuntime.InvokeVoidAsync("stopAllAudio");
        }

        async Task ReloadData()
        {
            DetectionDataGrid.Reset();
            await DetectionDataGrid.Reload();
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
                fromDate = toDate.Adjust(Filters.Timeframe);

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
                    ValidationMessage = ValidatorUtilities.GetInnerMessage(exception);
                else
                    // Report any other errors as unknown
                    LogAndReportUnknownException(exception);
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
