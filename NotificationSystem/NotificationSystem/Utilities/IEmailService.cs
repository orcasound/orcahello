using Amazon.SimpleEmail.Model;
using System.Threading.Tasks;

namespace NotificationSystem.Utilities
{
    public interface IEmailService
    {
        Task SendEmailAsync(SendEmailRequest request);
    }
}
