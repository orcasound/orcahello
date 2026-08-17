namespace AIForOrcas.DTO
{
    public class PaginatedResponseDTO<T>
    {
        public T Response { get; set; }
        public int TotalAmountPages { get; set; }
        public int TotalNumberRecords { get; set; }
    }
}
