namespace AIForOrcas.Server.Helpers;

public static class DetectionFilters
{
    public static int DefaultRecordsPerPage = 5;

    public static void ApplyTimestampSortFilter(ref List<Detection> list, string sortOrder)
    {
        if (sortOrder == "asc")
        {
            list = list.OrderBy(x => x.Timestamp)
                .ThenByDescending(x => x.Confidence)
                .ThenBy(x => x.Id)
                .ToList();
        }

        if (sortOrder == "desc")
        {
            list = list.OrderByDescending(x => x.Timestamp)
                .ThenByDescending(x => x.Confidence)
                .ThenBy(x => x.Id)
                .ToList();
        }
    }

    public static void ApplyConfidenceSortFilter(ref List<Detection> list, string sortOrder)
    {
        if (sortOrder == "asc")
        {
            list = list.OrderBy(x => x.Confidence)
                .ThenBy(x => x.Timestamp)
                .ThenBy(x => x.Id)
                .ToList();
        }

        if (sortOrder == "desc")
        {
            list = list.OrderByDescending(x => x.Confidence)
                .ThenBy(x => x.Timestamp)
                .ThenBy(x => x.Id)
                .ToList();
        }
    }

    public static void ApplyPaginationFilter(ref List<Detection> list, int page, int take)
    {
        var skip = page > 0 ? page - 1 : 0;
        var recordsPerPage = take > 0 ? take : DefaultRecordsPerPage;

        list = list
            .Skip(skip * recordsPerPage)
            .Take(recordsPerPage)
            .ToList();
    }
}
