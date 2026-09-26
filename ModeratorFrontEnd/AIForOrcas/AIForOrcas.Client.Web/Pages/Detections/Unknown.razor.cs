using AIForOrcas.Client.Web.Models;
using Microsoft.AspNetCore.Components;

namespace AIForOrcas.Client.Web.Pages.Detections;

public partial class Unknown
{
    public Unknown()
    {
        filterOptions = new CandidateFilterOptionsDTO() { SortBy = "timestamp", SortOrder = "desc", Timeframe = "24h", Location = "all", HydrophoneId = "all" };
    }

    protected override async Task<PaginatedResponseDTO<List<Detection>>> FetchDetectionsAsync(PaginationOptionsDTO paginationOptions, CandidateFilterOptionsDTO filterOptions)
    {
        return await Service.GetUnconfirmedDetectionsAsync(paginationOptions, filterOptions);
    }
}
