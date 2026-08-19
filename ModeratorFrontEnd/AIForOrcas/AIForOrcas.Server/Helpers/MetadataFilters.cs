namespace AIForOrcas.Server.Helpers;

public static class MetadataFilters
{
    public static int DefaultRecordsPerPage = 5;

    public static void ApplyTimeframeFilter(ref IQueryable<Metadata> queryable, string timeframe, DateTime? dateFrom = null, DateTime? dateTo = null)
    {
        if (!string.IsNullOrWhiteSpace(timeframe))
        {
            timeframe = timeframe.ToLower();

            if (timeframe != "all")
            {
                if (timeframe == "range")
                {
                    if (dateFrom != null && dateTo != null)
                    {
                        queryable = queryable.Where(x => x.timestamp >= dateFrom && x.timestamp <= dateTo);
                    }
                    else if (dateFrom == null && dateTo != null)
                    {
                        queryable = queryable.Where(x => x.timestamp <= dateTo);
                    }
                    else if (dateFrom != null && dateTo == null)
                    {
                        queryable = queryable.Where(x => x.timestamp >= dateFrom);
                    }
                }
                else
                {
                    var now = DateTime.Now;

                    switch (timeframe)
                    {
                        case "30m":
                            now = now.AddMinutes(-30);
                            break;

                        case "3h":
                            now = now.AddHours(-3);
                            break;

                        case "6h":
                            now = now.AddHours(-6);
                            break;

                        case "24h":
                            now = now.AddHours(-24);
                            break;

                        case "1w":
                            now = now.AddDays(-7);
                            break;

                        case "1m":
                            now = now.AddDays(-30);
                            break;
                    }
                    queryable = queryable.Where(x => x.timestamp >= now);
                }
            }
        }
    }

    public static void ApplyModeratorFilter(ref IQueryable<Metadata> queryable, string moderator)
    {
        if (!string.IsNullOrWhiteSpace(moderator))
        {
            queryable = queryable.Where(x => x.moderator == moderator);
        }
    }

    public static void ApplyLocationFilter(ref IQueryable<Metadata> queryable, string location)
    {
        if (!string.IsNullOrWhiteSpace(location))
        {
            queryable = queryable.Where(x => x.location.name == location);
        }
    }

    public static void ApplyHydrophoneIdFilter(ref IQueryable<Metadata> queryable, string hydrophoneId)
    {
        if (!string.IsNullOrWhiteSpace(hydrophoneId))
        {
            queryable = queryable.Where(x => x.source_guid == hydrophoneId);
        }
    }

    public static void ApplyReviewedFilter(ref IQueryable<Metadata> queryable, bool reviewed)
    {
        queryable = queryable.Where(x => x.reviewed == reviewed);
    }

    public static void ApplyFoundFilter(ref IQueryable<Metadata> queryable, string foundState)
    {
        queryable = queryable.Where(x => x.SRKWFound == foundState);
    }
}
