namespace AIForOrcas.DTO
{
    public class PaginationOptionsDTO
    {
        public int Page { get; set; } = 1;

        public int RecordsPerPage { get; set; } = 10;

        public int Radius { get; set; } = 3;

        public string QueryString { get => $"page={Page}&recordsPerPage={RecordsPerPage}"; }
    }
}
