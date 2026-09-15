using Amazon.SimpleEmail.Model;
using Azure;
using Azure.Data.Tables;
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

                var tableClientMock = new Mock<TableClient>();
                tableClientMock
                    .Setup(x => x.GetEntityAsync<SubscriberNotificationCooldownEntity>(
                        "SubscriberNotificationCooldown", "rpi_mast_center", null, default))
                    .ThrowsAsync(new RequestFailedException(404, "Not Found"));
                tableClientMock
                    .Setup(x => x.UpsertEntityAsync(It.IsAny<SubscriberNotificationCooldownEntity>(), It.IsAny<TableUpdateMode>(), default))
                    .ReturnsAsync(Mock.Of<Response>());

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
                            id = "RPI_Mast_Center",
                            name = "Mast Center",
                            latitude = 47.0,
                            longitude = -122.0,
                        },
                        moderator = "Test Moderator",
                        comments = "Looks good"
                    })
                };
                var recipients = new List<SubscriberEmailEntity> { new("subscriber@example.com") };

                await function.ProcessMessagesAsync(tableClientMock.Object, messages, recipients);

                emailServiceMock.Verify(
                    x => x.SendEmailAsync(It.Is<SendEmailRequest>(request =>
                        request.Source == "sender@example.com" &&
                        request.Destination.ToAddresses.Contains("subscriber@example.com") &&
                        request.Message.Subject.Data.Contains("Mast Center"))),
                    Times.Once);

                tableClientMock.Verify(
                    x => x.UpsertEntityAsync(
                        It.Is<SubscriberNotificationCooldownEntity>(e => e.RowKey == "rpi_mast_center"),
                        It.IsAny<TableUpdateMode>(), default),
                    Times.Once);
            }
            finally
            {
                Environment.SetEnvironmentVariable("SenderEmail", previousSenderEmail);
            }
        }

        [Theory]
        [InlineData(5, false)]
        [InlineData(20, true)]
        public async Task ProcessMessagesAsync_RespectsCooldownWindow(int minutesSinceLastSent, bool expectSend)
        {
            await RunCooldownScenario(null, minutesSinceLastSent, expectSend);
        }

        [Theory]
        [InlineData("5", 3, false)]
        [InlineData("5", 10, true)]
        [InlineData("not-a-number", 5, false)]
        [InlineData("0", 5, false)]
        [InlineData("-5", 5, false)]
        public async Task ProcessMessagesAsync_ParsesCooldownMinutesFromConfig(string configuredCooldownMinutes, int minutesSinceLastSent, bool expectSend)
        {
            await RunCooldownScenario(configuredCooldownMinutes, minutesSinceLastSent, expectSend);
        }

        private async Task RunCooldownScenario(string? configuredCooldownMinutes, int minutesSinceLastSent, bool expectSend)
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

                var lastSentAt = DateTimeOffset.UtcNow.AddMinutes(-minutesSinceLastSent);
                var cooldownEntity = new SubscriberNotificationCooldownEntity("RPI_Mast_Center") { LastSentAt = lastSentAt };
                var tableClientMock = new Mock<TableClient>();
                tableClientMock
                    .Setup(x => x.GetEntityAsync<SubscriberNotificationCooldownEntity>(
                        "SubscriberNotificationCooldown", "rpi_mast_center", null, default))
                    .ReturnsAsync(Response.FromValue(cooldownEntity, Mock.Of<Response>()));
                tableClientMock
                    .Setup(x => x.UpsertEntityAsync(It.IsAny<SubscriberNotificationCooldownEntity>(), It.IsAny<TableUpdateMode>(), default))
                    .ReturnsAsync(Mock.Of<Response>());

                // Default cooldown window is 15 minutes when SUBSCRIBER_EMAIL_COOLDOWN_MINUTES isn't configured
                var configuration = new ConfigurationBuilder()
                    .AddInMemoryCollection(configuredCooldownMinutes is null
                        ? Array.Empty<KeyValuePair<string, string?>>()
                        : new[] { new KeyValuePair<string, string?>("SUBSCRIBER_EMAIL_COOLDOWN_MINUTES", configuredCooldownMinutes) })
                    .Build();
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
                            id = "RPI_Mast_Center",
                            name = "Mast Center",
                            latitude = 47.0,
                            longitude = -122.0,
                        },
                        moderator = "Test Moderator",
                        comments = "Looks good"
                    })
                };
                var recipients = new List<SubscriberEmailEntity> { new("subscriber@example.com") };

                await function.ProcessMessagesAsync(tableClientMock.Object, messages, recipients);

                emailServiceMock.Verify(
                    x => x.SendEmailAsync(It.IsAny<SendEmailRequest>()),
                    expectSend ? Times.Once : Times.Never);

                tableClientMock.Verify(
                    x => x.UpsertEntityAsync(
                        It.Is<SubscriberNotificationCooldownEntity>(e => e.RowKey == "rpi_mast_center"),
                        It.IsAny<TableUpdateMode>(), default),
                    expectSend ? Times.Once : Times.Never);
            }
            finally
            {
                Environment.SetEnvironmentVariable("SenderEmail", previousSenderEmail);
            }
        }

        [Fact]
        public async Task ProcessMessagesAsync_LocationInCooldownDoesNotBlockOtherLocations()
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
                orcasiteHelperMock.Setup(x => x.GetSlugByLocationName(It.IsAny<string>())).Returns("slug");

                var cooldownEntity = new SubscriberNotificationCooldownEntity("RPI_Mast_Center")
                {
                    LastSentAt = DateTimeOffset.UtcNow.AddMinutes(-5)
                };
                var tableClientMock = new Mock<TableClient>();
                tableClientMock
                    .Setup(x => x.GetEntityAsync<SubscriberNotificationCooldownEntity>(
                        "SubscriberNotificationCooldown", "rpi_mast_center", null, default))
                    .ReturnsAsync(Response.FromValue(cooldownEntity, Mock.Of<Response>()));
                tableClientMock
                    .Setup(x => x.GetEntityAsync<SubscriberNotificationCooldownEntity>(
                        "SubscriberNotificationCooldown", "rpi_orcasound_lab", null, default))
                    .ThrowsAsync(new RequestFailedException(404, "Not Found"));
                tableClientMock
                    .Setup(x => x.UpsertEntityAsync(It.IsAny<SubscriberNotificationCooldownEntity>(), It.IsAny<TableUpdateMode>(), default))
                    .ReturnsAsync(Mock.Of<Response>());

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
                        location = new { id = "RPI_Mast_Center", name = "Mast Center", latitude = 47.0, longitude = -122.0 },
                        moderator = "Test Moderator",
                        comments = "Looks good"
                    }),
                    JObject.FromObject(new
                    {
                        timestamp = DateTime.UtcNow,
                        location = new { id = "RPI_Orcasound_Lab", name = "Orcasound Lab", latitude = 48.0, longitude = -123.0 },
                        moderator = "Test Moderator",
                        comments = "Looks good"
                    })
                };
                var recipients = new List<SubscriberEmailEntity> { new("subscriber@example.com") };

                await function.ProcessMessagesAsync(tableClientMock.Object, messages, recipients);

                emailServiceMock.Verify(
                    x => x.SendEmailAsync(It.Is<SendEmailRequest>(request =>
                        request.Message.Subject.Data.Contains("Mast Center"))),
                    Times.Never);
                emailServiceMock.Verify(
                    x => x.SendEmailAsync(It.Is<SendEmailRequest>(request =>
                        request.Message.Subject.Data.Contains("Orcasound Lab"))),
                    Times.Once);

                tableClientMock.Verify(
                    x => x.UpsertEntityAsync(
                        It.Is<SubscriberNotificationCooldownEntity>(e => e.RowKey == "rpi_mast_center"),
                        It.IsAny<TableUpdateMode>(), default),
                    Times.Never);
                tableClientMock.Verify(
                    x => x.UpsertEntityAsync(
                        It.Is<SubscriberNotificationCooldownEntity>(e => e.RowKey == "rpi_orcasound_lab"),
                        It.IsAny<TableUpdateMode>(), default),
                    Times.Once);
            }
            finally
            {
                Environment.SetEnvironmentVariable("SenderEmail", previousSenderEmail);
            }
        }

        [Fact]
        public async Task ProcessMessagesAsync_FallsBackToUnknownWhenLocationIdIsMissing()
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
                orcasiteHelperMock.Setup(x => x.GetSlugByLocationName(It.IsAny<string>())).Returns("slug");

                var tableClientMock = new Mock<TableClient>();
                tableClientMock
                    .Setup(x => x.GetEntityAsync<SubscriberNotificationCooldownEntity>(
                        "SubscriberNotificationCooldown", "unknown", null, default))
                    .ThrowsAsync(new RequestFailedException(404, "Not Found"));
                tableClientMock
                    .Setup(x => x.UpsertEntityAsync(It.IsAny<SubscriberNotificationCooldownEntity>(), It.IsAny<TableUpdateMode>(), default))
                    .ReturnsAsync(Mock.Of<Response>());

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
                        location = new { name = "Mast Center", latitude = 47.0, longitude = -122.0 },
                        moderator = "Test Moderator",
                        comments = "Looks good"
                    })
                };
                var recipients = new List<SubscriberEmailEntity> { new("subscriber@example.com") };

                await function.ProcessMessagesAsync(tableClientMock.Object, messages, recipients);

                emailServiceMock.Verify(
                    x => x.SendEmailAsync(It.IsAny<SendEmailRequest>()),
                    Times.Once);

                tableClientMock.Verify(
                    x => x.UpsertEntityAsync(
                        It.Is<SubscriberNotificationCooldownEntity>(e => e.RowKey == "unknown"),
                        It.IsAny<TableUpdateMode>(), default),
                    Times.Once);
            }
            finally
            {
                Environment.SetEnvironmentVariable("SenderEmail", previousSenderEmail);
            }
        }
    }
}
