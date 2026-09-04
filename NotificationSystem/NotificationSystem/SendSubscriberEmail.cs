using Azure;
using Azure.Data.Tables;
using Azure.Storage.Queues;
using Azure.Storage.Queues.Models;
using ComposableAsync;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Newtonsoft.Json.Linq;
using NotificationSystem.Models;
using NotificationSystem.Template;
using NotificationSystem.Utilities;
using RateLimiter;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace NotificationSystem
{
    public class SendSubscriberEmail
    {
        private readonly ILogger _logger;
        private readonly OrcasiteHelper _orcasiteHelper;
        private readonly IConfiguration _configuration;
        private readonly IEmailService _emailService;
        const int SendRate = 14;

        public SendSubscriberEmail(ILogger<SendSubscriberEmail> logger, OrcasiteHelper orcasiteHelper, IConfiguration configuration, IEmailService emailService)
        {
            _logger = logger;
            _orcasiteHelper = orcasiteHelper;
            _configuration = configuration;
            _emailService = emailService;
        }

        private async Task<bool> IsInCoolDownAsync(TableClient tableClient, string location)
        {
            int cooldownMinutes = int.TryParse(_configuration["SUBSCRIBER_EMAIL_COOLDOWN_MINUTES"], out var configurationCooldown) ? configurationCooldown : 15;
            try
            {
                _logger.LogInformation("Retrieving the last notification sent time at the location");

                var respone = await tableClient.GetEntityAsync<SubscriberNotificationCooldownEntity>(
                    "SubscriberNotificationCooldown", location.ToLowerInvariant());

                _logger.LogInformation($"IsInCoolDown: {DateTimeOffset.UtcNow - respone.Value.LastSentAt < TimeSpan.FromMinutes(cooldownMinutes)}");

                return DateTimeOffset.UtcNow - respone.Value.LastSentAt < TimeSpan.FromMinutes(cooldownMinutes);
            }
            catch (RequestFailedException ex) when (ex.Status == 404)
            {
                return false;
            }
        }

        public async Task ProcessMessagesAsync(
            TableClient tableClient,
            List<JObject> messages,
            IEnumerable<SubscriberEmailEntity> emailEntities)
        {
            var subscribers = emailEntities.ToList();
            var timeConstraint = TimeLimiter.GetFromMaxCountByInterval(SendRate, TimeSpan.FromSeconds(1));
            _logger.LogInformation("Retrieving email list and sending notifications");
            foreach (var message in messages)
            {
                string location = EmailTemplate.GetLocation(message) ?? "Unknown";

                if (await IsInCoolDownAsync(tableClient, location))
                {
                    _logger.LogInformation($"Skipping Notification for {location}: within cooldown window");
                    continue;
                }

                _logger.LogInformation($"Sending Notification for {location}: outside cooldown window");

                string category = "Southern Resident Killer Whale";
                string emailSubject = EmailTemplate.GetSubscriberEmailSubject(category, location);
                string body = CreateBody(message, category);
                foreach (var emailEntity in subscribers)
                {
                    await timeConstraint;
                    var email = EmailHelpers.CreateEmail(
                        Environment.GetEnvironmentVariable("SenderEmail"),
                        emailEntity.Email,
                        emailSubject,
                        body);
                    await _emailService.SendEmailAsync(email);
                }

                await tableClient.UpsertEntityAsync(new SubscriberNotificationCooldownEntity(location)
                {
                    LastSentAt = DateTimeOffset.UtcNow
                });
            }
        }

        [Function("SendSubscriberEmail")]
        // TODO: change timer to once per hour (0 0 * * * *)
        public async Task Run(
            [TimerTrigger("0 */1 * * * *")] string timerInfo,
            [TableInput("EmailList", Connection = "OrcaNotificationStorageSetting")] TableClient tableClient)
        {
            // Initialize OrcasiteHelper to fetch feeds data
            await _orcasiteHelper.InitializeAsync(_configuration);
            
            string queueConnection = Environment.GetEnvironmentVariable("OrcaNotificationStorageSetting");
            var queueClient = new QueueClient(queueConnection, "srkwfound");

            _logger.LogInformation("Checking if there are items in queue");
            QueueProperties properties = await queueClient.GetPropertiesAsync();

            if (properties.ApproximateMessagesCount == 0)
            {
                _logger.LogInformation("No items in queue");
                return;
            }

            _logger.LogInformation("Creating email message");
            List<JObject> messages = await GetMessages(queueClient);

            var emailEntities = await EmailHelpers.GetEmailEntitiesAsync<SubscriberEmailEntity>(tableClient, "Subscriber");
            await ProcessMessagesAsync(tableClient, messages, emailEntities);
        }

        private async Task<List<JObject>> GetMessages(QueueClient queueClient)
        {
            QueueMessage message;
            List<JObject> messagesJson = new List<JObject>();
            while (true)
            {
                var response = await queueClient.ReceiveMessageAsync();
                message = response.Value;
                if (message == null || string.IsNullOrEmpty(message.MessageText))
                    break;
                var decoded = Encoding.UTF8.GetString(Convert.FromBase64String(message.MessageText));
                messagesJson.Add(JsonConvert.DeserializeObject<JObject>(decoded));
                await queueClient.DeleteMessageAsync(message.MessageId, message.PopReceipt);
            }
            return messagesJson;
        }

        private string CreateBody(JObject messageJson, string category)
        {
            return EmailTemplate.GetSubscriberEmailBody(messageJson, category, _orcasiteHelper);
        }
    }
}