using Amazon.SimpleEmail.Model;
using Microsoft.Extensions.Logging;
using Moq;
using NotificationSystem.Models;
using NotificationSystem.Utilities;
using System.Text.Json;

namespace NotificationSystem.Tests.Integration
{
    [Collection(SenderEmailTestCollection.Name)]
    public class SendModeratorEmailIntegrationTests
    {
        [Fact]
        public async Task ProcessDocumentsAsync_SendsEmail_ForUnreviewedDetection()
        {
            string? previousSenderEmail = Environment.GetEnvironmentVariable("SenderEmail");
            Environment.SetEnvironmentVariable("SenderEmail", "sender@example.com");

            try
            {
                var emailServiceMock = new Mock<IEmailService>();
                emailServiceMock.Setup(x => x.SendEmailAsync(It.IsAny<SendEmailRequest>()))
                    .Returns(Task.CompletedTask);

                var loggerMock = new Mock<ILogger<SendModeratorEmail>>();
                var function = new SendModeratorEmail(loggerMock.Object, emailServiceMock.Object);

                var input = new List<JsonElement>
                {
                    JsonSerializer.SerializeToElement(new
                    {
                        reviewed = false,
                        timestamp = DateTime.UtcNow,
                        location = new { name = "Mast Center" }
                    })
                };
                var recipients = new List<ModeratorEmailEntity> { new("moderator@example.com") };

                await function.ProcessDocumentsAsync(input, recipients);

                emailServiceMock.Verify(
                    x => x.SendEmailAsync(It.Is<SendEmailRequest>(request =>
                        request.Source == "sender@example.com" &&
                        request.Destination.ToAddresses.Contains("moderator@example.com") &&
                        request.Message.Subject.Data.Contains("Mast Center"))),
                    Times.Once);
            }
            finally
            {
                Environment.SetEnvironmentVariable("SenderEmail", previousSenderEmail);
            }
        }

        [Fact]
        public async Task ProcessDocumentsAsync_DoesNotSendEmail_WhenAllReviewed()
        {
            var emailServiceMock = new Mock<IEmailService>();
            emailServiceMock.Setup(x => x.SendEmailAsync(It.IsAny<SendEmailRequest>()))
                .Returns(Task.CompletedTask);

            var loggerMock = new Mock<ILogger<SendModeratorEmail>>();
            var function = new SendModeratorEmail(loggerMock.Object, emailServiceMock.Object);

            var input = new List<JsonElement>
            {
                JsonSerializer.SerializeToElement(new
                {
                    reviewed = true,
                    timestamp = DateTime.UtcNow,
                    location = new { name = "Mast Center" }
                })
            };
            var recipients = new List<ModeratorEmailEntity> { new("moderator@example.com") };

            await function.ProcessDocumentsAsync(input, recipients);

            emailServiceMock.Verify(x => x.SendEmailAsync(It.IsAny<SendEmailRequest>()), Times.Never);
        }

        [Fact]
        public async Task ProcessDocumentsAsync_SendsEmail_WhenReviewedFieldMissing()
        {
            string? previousSenderEmail = Environment.GetEnvironmentVariable("SenderEmail");
            Environment.SetEnvironmentVariable("SenderEmail", "sender@example.com");

            try
            {
                var emailServiceMock = new Mock<IEmailService>();
                emailServiceMock.Setup(x => x.SendEmailAsync(It.IsAny<SendEmailRequest>()))
                    .Returns(Task.CompletedTask);

                var loggerMock = new Mock<ILogger<SendModeratorEmail>>();
                var function = new SendModeratorEmail(loggerMock.Object, emailServiceMock.Object);

                var input = new List<JsonElement>
                {
                    JsonSerializer.SerializeToElement(new
                    {
                        timestamp = DateTime.UtcNow,
                        location = new { name = "Bush Point" }
                    })
                };
                var recipients = new List<ModeratorEmailEntity> { new("moderator@example.com") };

                await function.ProcessDocumentsAsync(input, recipients);

                emailServiceMock.Verify(
                    x => x.SendEmailAsync(It.Is<SendEmailRequest>(request =>
                        request.Source == "sender@example.com" &&
                        request.Destination.ToAddresses.Contains("moderator@example.com") &&
                        request.Message.Subject.Data.Contains("Bush Point"))),
                    Times.Once);
            }
            finally
            {
                Environment.SetEnvironmentVariable("SenderEmail", previousSenderEmail);
            }
        }
    }
}
