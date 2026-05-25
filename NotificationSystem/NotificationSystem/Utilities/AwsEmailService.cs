using Amazon;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using System.Threading.Tasks;

namespace NotificationSystem.Utilities
{
    public class AwsEmailService : IEmailService
    {
        private readonly AmazonSimpleEmailServiceClient _client;

        public AwsEmailService()
        {
            _client = new AmazonSimpleEmailServiceClient(RegionEndpoint.USWest2);
        }

        public async Task SendEmailAsync(SendEmailRequest request)
        {
            await _client.SendEmailAsync(request);
        }
    }
}
