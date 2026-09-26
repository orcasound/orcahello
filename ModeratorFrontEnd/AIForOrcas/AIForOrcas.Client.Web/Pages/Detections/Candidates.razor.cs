using AIForOrcas.Client.Web.Models;
using Microsoft.AspNetCore.Components;

namespace AIForOrcas.Client.Web.Pages.Detections;

public partial class Candidates
{
    public Candidates()
    {
        // Initialize page-specific defaults
        filterOptions = new CandidateFilterOptionsDTO() { SortBy = "timestamp", SortOrder = "desc", Timeframe = "6h", Location = "all", HydrophoneId = "all" };
    }

    protected override async Task<PaginatedResponseDTO<List<Detection>>> FetchDetectionsAsync(PaginationOptionsDTO paginationOptions, CandidateFilterOptionsDTO filterOptions)
    {
        return await Service.GetCandidateDetectionsAsync(paginationOptions, filterOptions);
    }

    protected override void SetEmptyLoadStatus(PaginatedResponseDTO<List<Detection>> paginatedResponse)
    {
        loadStatus = paginatedResponse.TotalNumberRecords == 0
            ? "You're caught up, no records match the selected filter options..."
            : "No records found for the selected filter options. Please select a different set of filter options...";
    }

    protected override async Task ActOnSubmitCallback(DetectionUpdate request)
    {
        int submittedIndex = detectionMinutes?.FindIndex(m => m.Detections.Any(d => d.Id == request.Id)) ?? -1;
        int pageBefore = paginationOptions.Page;

        await Service.UpdateRequestAsync(request);

        List<string> leafTags = Detection.GetLeafTags(request.Tags);
        TagCache.SetTags(_userId, leafTags);

        await JSRuntime.InvokeVoidAsync("DestroyActivePlayer");
        await LoadDetections();

        if (submittedIndex >= 0 && detectionMinutes != null && detectionMinutes.Count > 0)
        {
            // Same page: the card that moved up into the submitted slot (or the
            // last one, if that slot is gone). A different page: start at its top.
            int nextIndex = paginationOptions.Page == pageBefore
                ? Math.Min(submittedIndex, detectionMinutes.Count - 1)
                : 0;
            _scrollToDetectionId = detectionMinutes[nextIndex].Id;
        }
    }

}
