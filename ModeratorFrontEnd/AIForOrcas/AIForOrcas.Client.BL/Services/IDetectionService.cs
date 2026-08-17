using AIForOrcas.DTO;
using AIForOrcas.DTO.API;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AIForOrcas.Client.BL.Services
{
    public interface IDetectionService
    {
        Task<PaginatedResponseDTO<List<Detection>>> GetCandidateDetectionsAsync(PaginationOptionsDTO paginationOptions, IFilterOptions filterOptions);
        Task<PaginatedResponseDTO<List<Detection>>> GetConfirmedDetectionsAsync(PaginationOptionsDTO pagination, IFilterOptions filterOptions);
        Task<PaginatedResponseDTO<List<Detection>>> GetUnconfirmedDetectionsAsync(PaginationOptionsDTO pagination, IFilterOptions filterOptions);
        Task<PaginatedResponseDTO<List<Detection>>> GetFalseDetectionsAsync(PaginationOptionsDTO pagination, IFilterOptions filterOptions);

        Task<Detection> GetDetectionAsync(string id);
        Task UpdateRequestAsync(DetectionUpdate request);
    }
}
