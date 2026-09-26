using AIForOrcas.Client.Web.Models;
using Microsoft.AspNetCore.Components;

namespace AIForOrcas.Client.Web.Pages.Detections;

public partial class FalsePositives
{
    public FalsePositives()
    {
        filterOptions = new ReviewedFilterOptionsDTO() { SortBy = "timestamp", SortOrder = "desc", Timeframe = "24h", Location = "all", HydrophoneId = "all" };
    }

    protected override async Task<PaginatedResponseDTO<List<Detection>>> FetchDetectionsAsync(PaginationOptionsDTO paginationOptions, ReviewedFilterOptionsDTO filterOptions)
    {
        return await Service.GetFalseDetectionsAsync(paginationOptions, filterOptions);
    }
}
