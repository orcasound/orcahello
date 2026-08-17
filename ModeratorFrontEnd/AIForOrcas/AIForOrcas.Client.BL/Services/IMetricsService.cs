using AIForOrcas.DTO;
using AIForOrcas.DTO.API;
using System.Threading.Tasks;

namespace AIForOrcas.Client.BL.Services
{
    public interface IMetricsService
    {

        Task<Metrics> GetSiteMetricsAsync(IFilterOptions filterOptions);
        Task<ModeratorMetrics> GetModeratorMetricsAsync(IFilterOptions filterOptions);
    }
}
