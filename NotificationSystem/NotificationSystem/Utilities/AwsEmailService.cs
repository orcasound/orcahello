using Amazon;
using Amazon.SimpleEmail;
using Amazon.SimpleEmail.Model;
using System;
using System.Threading.Tasks;

namespace NotificationSystem.Utilities
{
    public class AwsEmailService : IEmailService, IDisposable
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

        public void Dispose()
        {
            _client.Dispose();
        }
    }
}
