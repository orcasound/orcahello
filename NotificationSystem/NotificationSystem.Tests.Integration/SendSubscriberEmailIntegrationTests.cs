using Amazon.SimpleEmail.Model;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Moq;
using Newtonsoft.Json.Linq;
using NotificationSystem.Models;
using NotificationSystem.Utilities;

namespace NotificationSystem.Tests.Integration
{
    [Collection(SenderEmailTestCollection.Name)]
    public class SendSubscriberEmailIntegrationTests
    {
        [Fact]
        public async Task ProcessMessagesAsync_SendsSubscriberEmail()
        {
            var previousSenderEmail = Environment.GetEnvironmentVariable("SenderEmail");
            Environment.SetEnvironmentVariable("SenderEmail", "sender@example.com");

            try
            {
                var emailServiceMock = new Mock<IEmailService>();
                emailServiceMock.Setup(x => x.SendEmailAsync(It.IsAny<SendEmailRequest>()))
                    .Returns(Task.CompletedTask);

                var loggerMock = new Mock<ILogger<SendSubscriberEmail>>();
                var orcasiteLoggerMock = new Mock<ILogger<OrcasiteHelper>>();
                var orcasiteHelperMock = new Mock<OrcasiteHelper>(orcasiteLoggerMock.Object, new HttpClient());
                orcasiteHelperMock.Setup(x => x.GetSlugByLocationName(It.IsAny<string>())).Returns("mast-center");

                var configuration = new ConfigurationBuilder().Build();
                var function = new SendSubscriberEmail(
                    loggerMock.Object,
                    orcasiteHelperMock.Object,
                    configuration,
                    emailServiceMock.Object);

                var messages = new List<JObject>
                {
                    JObject.FromObject(new
                    {
                        timestamp = DateTime.UtcNow,
                        location = new
                        {
                            name = "Mast Center",
                            latitude = 47.0,
                            longitude = -122.0,
                        },
                        moderator = "Test Moderator",
                        comments = "Looks good"
                    })
                };
                var recipients = new List<SubscriberEmailEntity> { new("subscriber@example.com") };

                await function.ProcessMessagesAsync(messages, recipients);

                emailServiceMock.Verify(
                    x => x.SendEmailAsync(It.Is<SendEmailRequest>(request =>
                        request.Source == "sender@example.com" &&
                        request.Destination.ToAddresses.Contains("subscriber@example.com") &&
                        request.Message.Subject.Data.Contains("Mast Center"))),
                    Times.Once);
            }
            finally
            {
                Environment.SetEnvironmentVariable("SenderEmail", previousSenderEmail);
            }
        }
    }
}
