using System.Threading.Tasks;

namespace NotificationSystem.Utilities
{
    public interface IFacebookPostService
    {
        Task<bool> PostToPageAsync(string message);
    }
}