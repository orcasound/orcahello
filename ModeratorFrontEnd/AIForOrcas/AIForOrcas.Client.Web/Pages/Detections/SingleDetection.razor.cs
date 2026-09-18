using AIForOrcas.Client.Web.Models;
using AIForOrcas.DTO.API;

namespace AIForOrcas.Client.Web.Pages.Detections;

public partial class SingleDetection : ComponentBase, IDisposable
{
    [Parameter]
    public string Id { get; set; }

    [Inject]
    IJSRuntime JSRuntime { get; set; }

    [Inject]
    IDetectionService Service { get; set; }

    [Inject]
    IToastService ToastService { get; set; }

    [Inject]
    UserTagCache TagCache { get; set; }

    [Inject]
    AuthenticationStateProvider AuthenticationStateProvider { get; set; }

    private string _userId;
    private Detection detection = null;
    private DetectionMinute detectionMinute = null;
    private bool isFound = true;
    private bool isUnavailable = false;

    protected override async Task OnInitializedAsync()
    {
        await LoadDetection();

        var authState = await AuthenticationStateProvider.GetAuthenticationStateAsync();
        var user = authState.User;
        _userId = user.FindFirst("oid")?.Value;
    }

    private async Task LoadDetection()
    {
        detection = await Service.GetDetectionAsync(Id);
        isUnavailable = detection == null;
        if (!isUnavailable && detection.Id == null)
        {
            isFound = false;
            return;
        }

        // If we have a valid detection, fetch all detections that share the same minute and location.
        if (!isUnavailable)
        {
            // Compute minute start/end for the detection timestamp preserving Kind.
            var ts = detection.Timestamp;
            var minuteStart = new DateTime(ts.Year, ts.Month, ts.Day, ts.Hour, ts.Minute, 0, ts.Kind);
            var minuteEnd = minuteStart.AddMinutes(1);

            // Build pagination and filter options to hit the root GET endpoint with a date range and location filter.
            var pagination = new PaginationOptionsDTO
            {
                Page = 1,
                MinutesPerPage = 1
            };

            var filter = new ReviewedFilterOptionsDTO
            {
                SortBy = "timestamp",
                SortOrder = "desc",
                Timeframe = "range",
                Location = "all",
                HydrophoneId = string.IsNullOrWhiteSpace(detection.Location?.Id) ? "all" : detection.Location.Id,
                // The range filter's upper bound is inclusive, so back off one
                // tick rather than one second: timestamps can carry fractional
                // seconds and :59.250 still belongs to this minute.
                DateFrom = minuteStart,
                DateTo = minuteEnd.AddTicks(-1)
            };

            var result = await Service.GetDetectionsAsync(pagination, filter);

            // A null response is a failed request, not an empty minute. Show
            // the error state instead of quietly degrading to a one-detection
            // minute whose submit would skip the siblings.
            if (result?.Response == null)
            {
                isUnavailable = true;
                return;
            }
            var allDetections = result.Response;

            // Initialize detectionMinute using the returned detection set. The factory groups by minute+location;
            // when the hydrophone falls back to "all", several hydrophones can share the minute, so pick the
            // group that actually holds the requested detection.
            var minutes = DetectionMinute.CreateDetectionMinutes(allDetections);
            detectionMinute = minutes.FirstOrDefault(m => m.Detections.Any(d => d.Id == detection.Id))
                ?? new DetectionMinute { Detections = new List<Detection> { detection } };
        }
    }

    private async Task ActOnSubmitCallback(DetectionUpdate request)
    {
        await Service.UpdateRequestAsync(request);

        List<string> leafTags = Detection.GetLeafTags(request.Tags);
        TagCache.SetTags(_userId, leafTags);

        await LoadDetection();
    }

    void IDisposable.Dispose()
    {
        JSRuntime.InvokeVoidAsync("DestroyActivePlayer");
    }
}
