using Location = OrcaHello.Web.Api.Models.Location;

namespace OrcaHello.Web.Api.Services
{
    public partial class MetadataService : IMetadataService
    {
        private readonly IStorageBroker _storageBroker;
        private readonly ILogger<MetadataService> _logger;

        // Needed for unit testing wrapper to work properly
        public MetadataService() { }

        public MetadataService(IStorageBroker storageBroker,
            ILogger<MetadataService> logger)
        {
            _storageBroker = storageBroker;
            _logger = logger;
        }

        #region metadata

        public ValueTask<QueryableMetadataForTimeframe> RetrievePositiveMetadataForGivenTimeframeAsync(DateTime fromDate, DateTime toDate, int page, int pageSize) =>
         TryCatch(async () =>
         {
             Validate(fromDate, nameof(fromDate));
             Validate(toDate, nameof(toDate));

             fromDate = NormalizeStartDate(fromDate);
             toDate = NormalizeEndDate(toDate);

             page = NormalizePage(page);
             pageSize = NormalizePageSize(pageSize);

             ValidateDatesAreWithinRange(fromDate, toDate);

             ListMetadataAndCount queryResults = await _storageBroker.GetPositiveMetadataListByTimeframe(fromDate, toDate, page, pageSize);

             return new QueryableMetadataForTimeframe
             {
                 QueryableRecords = queryResults.PaginatedRecords.AsQueryable(),
                 TotalCount = queryResults.TotalCount,
                 FromDate = fromDate,
                 ToDate = toDate,
                 Page = page,
                 PageSize = pageSize
             };
         });

        public ValueTask<QueryableMetadataForTimeframe> RetrieveNegativeAndUnknownMetadataForGivenTimeframeAsync(DateTime fromDate, DateTime toDate, int page, int pageSize) =>
         TryCatch(async () =>
         {
             Validate(fromDate, nameof(fromDate));
             Validate(toDate, nameof(toDate));

             fromDate = NormalizeStartDate(fromDate);
             toDate = NormalizeEndDate(toDate);

             page = NormalizePage(page);
             pageSize = NormalizePageSize(pageSize);

             ValidateDatesAreWithinRange(fromDate, toDate);

             ListMetadataAndCount queryResults = await _storageBroker.GetNegativeAndUnknownMetadataListByTimeframe(fromDate, toDate, page, pageSize);

             return new QueryableMetadataForTimeframe
             {
                 QueryableRecords = queryResults.PaginatedRecords.AsQueryable(),
                 TotalCount = queryResults.TotalCount,
                 FromDate = fromDate,
                 ToDate = toDate,
                 Page = page,
                 PageSize = pageSize
             };
         });

        public ValueTask<QueryableMetadataForTimeframe> RetrieveUnreviewedMetadataForGivenTimeframeAsync(DateTime fromDate, DateTime toDate, int page, int pageSize) =>
        TryCatch(async () =>
        {
            Validate(fromDate, nameof(fromDate));
            Validate(toDate, nameof(toDate));

            fromDate = NormalizeStartDate(fromDate);
            toDate = NormalizeEndDate(toDate);

            page = NormalizePage(page);
            pageSize = NormalizePageSize(pageSize);

            ValidateDatesAreWithinRange(fromDate, toDate);

            ListMetadataAndCount queryResults = await _storageBroker.GetUnreviewedMetadataListByTimeframe(fromDate, toDate, page, pageSize);

            return new QueryableMetadataForTimeframe
            {
                QueryableRecords = queryResults.PaginatedRecords.AsQueryable(),
                TotalCount = queryResults.TotalCount,
                FromDate = fromDate,
                ToDate = toDate,
                Page = page,
                PageSize = pageSize
            };
        });

        public ValueTask<QueryableMetadataFiltered> RetrievePaginatedMetadataAsync(string state, DateTime fromDate, DateTime toDate, string sortBy, bool isDescending, string location, int page, int pageSize) =>
        TryCatch(async () =>
        {
            Validate(fromDate, nameof(fromDate));
            Validate(toDate, nameof(toDate));
            Validate(state, nameof(state));
            Validate(sortBy, nameof(sortBy));

            fromDate = NormalizeStartDate(fromDate);
            toDate = NormalizeEndDate(toDate);

            page = NormalizePage(page);
            pageSize = NormalizePageSize(pageSize);

            ValidateStateIsAcceptable(state);
            ValidateDatesAreWithinRange(fromDate, toDate);

            string sortOrder = GetSortOrder(isDescending);
            string sortByString = GetSortField(sortBy);

            ListMetadataAndCount queryResults = await _storageBroker.GetMetadataListFiltered(state, fromDate, toDate, sortByString, sortOrder, location, page, pageSize);

            return new QueryableMetadataFiltered
            {
                QueryableRecords = queryResults.PaginatedRecords.AsQueryable(),
                TotalCount = queryResults.TotalCount,
                FromDate = fromDate,
                ToDate = toDate,
                Page = page,
                PageSize = pageSize,
                State = state,
                SortBy = sortByString,
                SortOrder = sortOrder,
                Location = location
            };
        });

        public ValueTask<Metadata> RetrieveMetadataByIdAsync(string id) =>
        TryCatch(async () =>
        {
            Validate(id, nameof(id));

            return await _storageBroker.GetMetadataById(id);
        });

        public ValueTask<bool> RemoveMetadataByIdAndStateAsync(string id, string state) =>
        TryCatch(async () =>
        {
            Validate(id, nameof(id));
            Validate(state, nameof(state));
            ValidateStateIsAcceptable(state);

            return await _storageBroker.DeleteMetadataByIdAndState(id, state);
        });

        public ValueTask<bool> AddMetadataAsync(Metadata metadata) =>
        TryCatch(async () =>
        {
            ValidateMetadataOnCreate(metadata);

            return await _storageBroker.InsertMetadata(metadata);
        });

        public ValueTask<QueryableMetadata> RetrieveMetadataForTagAsync(string tag) =>
        TryCatch(async () =>
        {
            Validate(tag, nameof(tag));

            ListMetadataAndCount queryResults = await _storageBroker.GetAllMetadataListByTag(tag);

            return new QueryableMetadata
            {
                QueryableRecords = queryResults.PaginatedRecords.AsQueryable(),
                TotalCount = queryResults.TotalCount,
            };
        });

        public ValueTask<QueryableMetadata> RetrieveMetadataForInterestLabelAsync(string interestLabel) =>
        TryCatch(async () =>
        {
            Validate(interestLabel, nameof(interestLabel));

            ListMetadataAndCount queryResults = await _storageBroker.GetAllMetadataListByInterestLabel(interestLabel);

            return new QueryableMetadata
            {
                QueryableRecords = queryResults.PaginatedRecords.AsQueryable(),
                TotalCount = queryResults.TotalCount,
            };
        });

        public ValueTask<bool> UpdateMetadataAsync(Metadata metadata) =>
        TryCatch(async () =>
        {
            ValidateMetadataOnUpdate(metadata);

            return await _storageBroker.UpdateMetadataInPartition(metadata);
        });

        #endregion

        #region moderator

        public ValueTask<QueryableModerators> RetrieveModeratorsAsync() =>
        TryCatch(async () =>
        {
            List<string> moderatorList = await _storageBroker.GetModeratorList();

            return new QueryableModerators
            {
                QueryableRecords = moderatorList.AsQueryable(),
                TotalCount = moderatorList.Count
            };
        });

        public ValueTask<QueryableMetadataForTimeframeAndModerator> RetrievePositiveMetadataForGivenTimeframeAndModeratorAsync(DateTime fromDate, DateTime toDate,
            string moderator, int page, int pageSize) =>
        TryCatch(async () =>
        {
            Validate(fromDate, nameof(fromDate));
            Validate(toDate, nameof(toDate));
            Validate(moderator, nameof(moderator));

            fromDate = NormalizeStartDate(fromDate);
            toDate = NormalizeEndDate(toDate);

            page = NormalizePage(page);
            pageSize = NormalizePageSize(pageSize);

            ValidateDatesAreWithinRange(fromDate, toDate);

            ListMetadataAndCount queryResults = await _storageBroker.GetPositiveMetadataListByTimeframeAndModerator(fromDate, toDate, moderator, page, pageSize);

            return new QueryableMetadataForTimeframeAndModerator
            {
                QueryableRecords = queryResults.PaginatedRecords.AsQueryable(),
                TotalCount = queryResults.TotalCount,
                FromDate = fromDate,
                ToDate = toDate,
                Page = page,
                PageSize = pageSize,
                Moderator = moderator
            };
        });

        public ValueTask<QueryableMetadataForTimeframeAndModerator> RetrieveNegativeAndUnknownMetadataForGivenTimeframeAndModeratorAsync(DateTime fromDate, DateTime toDate,
            string moderator, int page, int pageSize) =>
        TryCatch(async () =>
        {
            Validate(fromDate, nameof(fromDate));
            Validate(toDate, nameof(toDate));

            fromDate = NormalizeStartDate(fromDate);
            toDate = NormalizeEndDate(toDate);

            page = NormalizePage(page);
            pageSize = NormalizePageSize(pageSize);

            ValidateDatesAreWithinRange(fromDate, toDate);

            ListMetadataAndCount queryResults = await _storageBroker.
                GetNegativeAndUnknownMetadataListByTimeframeAndModerator(fromDate, toDate, moderator, page, pageSize);

            return new QueryableMetadataForTimeframeAndModerator
            {
                QueryableRecords = queryResults.PaginatedRecords.AsQueryable(),
                TotalCount = queryResults.TotalCount,
                FromDate = fromDate,
                ToDate = toDate,
                Page = page,
                PageSize = pageSize,
                Moderator = moderator
            };
        });

        public ValueTask<MetricsSummaryForTimeframeAndModerator> RetrieveMetricsForGivenTimeframeAndModeratorAsync(DateTime fromDate, DateTime toDate,
            string moderator) =>
        TryCatch(async () =>
        {
            Validate(fromDate, nameof(fromDate));
            Validate(toDate, nameof(toDate));
            Validate(moderator, nameof(moderator));

            fromDate = NormalizeStartDate(fromDate);
            toDate = NormalizeEndDate(toDate);

            var queryResults = await _storageBroker.GetMetricsListByTimeframeAndModerator(fromDate, toDate, moderator);

            return new MetricsSummaryForTimeframeAndModerator
            {
                QueryableRecords = queryResults.AsQueryable(),
                FromDate = fromDate,
                ToDate = toDate,
                Moderator = moderator
            };
        });

        public ValueTask<QueryableTagsForTimeframeAndModerator> RetrieveTagsForGivenTimePeriodAndModeratorAsync(DateTime fromDate, DateTime toDate, string moderator) =>
        TryCatch(async () =>
        {
            Validate(fromDate, nameof(fromDate));
            Validate(toDate, nameof(toDate));
            Validate(moderator, nameof(moderator));

            fromDate = NormalizeStartDate(fromDate);
            toDate = NormalizeEndDate(toDate);

            ValidateDatesAreWithinRange(fromDate, toDate);

            List<string> TagList = await _storageBroker.GetTagListByTimeframeAndModerator(fromDate, toDate, moderator);

            return new QueryableTagsForTimeframeAndModerator
            {
                QueryableRecords = TagList.AsQueryable(),
                FromDate = fromDate,
                ToDate = toDate,
                TotalCount = TagList.Count,
                Moderator = moderator
            };
        });

        public ValueTask<QueryableMetadataForTimeframeTagAndModerator> RetrieveMetadataForGivenTimeframeTagAndModeratorAsync(DateTime fromDate, DateTime toDate,
            string moderator, string tag, int page, int pageSize) =>
        TryCatch(async () =>
        {
            Validate(moderator, nameof(moderator));
            Validate(tag, nameof(tag));
            Validate(fromDate, nameof(fromDate));
            Validate(toDate, nameof(toDate));

            fromDate = NormalizeStartDate(fromDate);
            toDate = NormalizeEndDate(toDate);

            page = NormalizePage(page);
            pageSize = NormalizePageSize(pageSize);

            ValidateDatesAreWithinRange(fromDate, toDate);

            ListMetadataAndCount queryResults = await _storageBroker.GetMetadataListByTimeframeTagAndModerator
                (fromDate, toDate, moderator, tag, page, pageSize);

            return new QueryableMetadataForTimeframeTagAndModerator
            {
                QueryableRecords = queryResults.PaginatedRecords.AsQueryable(),
                TotalCount = queryResults.TotalCount,
                FromDate = fromDate,
                ToDate = toDate,
                Page = page,
                PageSize = pageSize,
                Moderator = moderator,
                Tag = tag
            };
        });

        #endregion

        #region tags

        public ValueTask<QueryableTagsForTimeframe> RetrieveTagsForGivenTimePeriodAsync(DateTime fromDate, DateTime toDate) =>
         TryCatch(async () =>
         {
             Validate(fromDate, nameof(fromDate));
             Validate(toDate, nameof(toDate));

             fromDate = NormalizeStartDate(fromDate);
             toDate = NormalizeEndDate(toDate);

             ValidateDatesAreWithinRange(fromDate, toDate);

             List<string> TagList = await _storageBroker.GetTagListByTimeframe(fromDate, toDate);

             TagList.RemoveAll(x => string.IsNullOrWhiteSpace(x));

             return new QueryableTagsForTimeframe
             {
                 QueryableRecords = TagList.AsQueryable(),
                 FromDate = fromDate,
                 ToDate = toDate,
                 TotalCount = TagList.Count
             };
         });

        public ValueTask<QueryableMetadataForTimeframeAndTag> RetrieveMetadataForGivenTimeframeAndTagAsync(DateTime fromDate, DateTime toDate, string tag, int page, int pageSize) =>
        TryCatch(async () =>
        {
            Validate(fromDate, nameof(fromDate));
            Validate(toDate, nameof(toDate));
            Validate(tag, nameof(tag));

            ValidateTagContainsOnlyValidCharacters(tag);

            List<string> tags = new();
            string tagOperator = "";

            if (tag.Contains(','))
            {
                tagOperator = "AND";
                tags = tag.Split(',').ToList();
            }
            else if (tag.Contains('|'))
            {
                tagOperator = "OR";
                tags = tag.Split('|').ToList();
            }
            else
            {
                tags.Add(tag);
            }

            fromDate = NormalizeStartDate(fromDate);
            toDate = NormalizeEndDate(toDate);

            page = NormalizePage(page);
            pageSize = NormalizePageSize(pageSize);

            ValidateDatesAreWithinRange(fromDate, toDate);

            ListMetadataAndCount queryResults = await _storageBroker.GetMetadataListByTimeframeAndTag(fromDate, toDate, tags, tagOperator, page, pageSize);

            return new QueryableMetadataForTimeframeAndTag
            {
                QueryableRecords = queryResults.PaginatedRecords.AsQueryable(),
                TotalCount = queryResults.TotalCount,
                FromDate = fromDate,
                ToDate = toDate,
                Tag = tag,
                Page = page,
                PageSize = pageSize
            };
        });

        public ValueTask<QueryableTags> RetrieveAllTagsAsync() =>
        TryCatch(async () =>
        {
            List<string> TagList = await _storageBroker.GetAllTagList();

            TagList.RemoveAll(x => string.IsNullOrWhiteSpace(x));

            return new QueryableTags
            {
                QueryableRecords = TagList.AsQueryable(),
                TotalCount = TagList.Count
            };
        });

        #endregion

        #region interest labels

        public ValueTask<QueryableInterestLabels> RetrieveAllInterestLabelsAsync() =>
        TryCatch(async () =>
        {
            List<string> InterestLabelList = await _storageBroker.GetAllInterestLabels();


            return new QueryableInterestLabels
            {
                QueryableRecords = InterestLabelList.AsQueryable(),
                TotalCount = InterestLabelList.Count
            };
        });

        #endregion

        #region metrics

        public ValueTask<MetricsSummaryForTimeframe> RetrieveMetricsForGivenTimeframeAsync(DateTime fromDate, DateTime toDate) =>
        TryCatch(async () =>
        {
            Validate(fromDate, nameof(fromDate));
            Validate(toDate, nameof(toDate));

            fromDate = NormalizeStartDate(fromDate);
            toDate = NormalizeEndDate(toDate);

            var queryResults = await _storageBroker.GetMetricsListByTimeframe(fromDate, toDate);

            return new MetricsSummaryForTimeframe
            {
                QueryableRecords = queryResults.AsQueryable(),
                FromDate = fromDate,
                ToDate = toDate
            };
        });

        #endregion

        #region helpers

        [ExcludeFromCodeCoverage]
        private static string GetSortField(string sortBy)
        {
            // Add logic to map sortBy parameter to corresponding field in the Clip object
            // For example, map "date" to "DateAndTimeCollected"
            // You can add more cases for different sorting options
            return (sortBy.ToLower()) switch
            {
                "date" => "timestamp",
                "confidence" => "whaleFoundConfidence",
                "moderator" => "moderator",
                "moderateddate" => "dateModerated",
                _ => "timestamp"
            };
        }

        [ExcludeFromCodeCoverage]
        private static string GetSortOrder(bool isDescending)
        {
            return isDescending ? "DESC" : "ASC";
        }

        [ExcludeFromCodeCoverage]
        private static int NormalizePage(int page)
        {
            return page < 0 ? 1 : page;
        }

        [ExcludeFromCodeCoverage]
        private static int NormalizePageSize(int pageSize)
        {
            return pageSize < 0 ? 10 : pageSize;
        }

        // Start at midnight
        [ExcludeFromCodeCoverage]
        private static DateTime NormalizeStartDate(DateTime startDate)
        {
            return startDate.Date;
        }

        // Stop at 23:59:59
        [ExcludeFromCodeCoverage]
        private static DateTime NormalizeEndDate(DateTime endDate)
        {
            return endDate.Date.AddHours(23).AddMinutes(59).AddSeconds(59);
        }

        #endregion

    }
}
