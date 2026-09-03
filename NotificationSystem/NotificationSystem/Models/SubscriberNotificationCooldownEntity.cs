using Azure;
using Azure.Data.Tables;
using System;

namespace NotificationSystem.Models
{
    public class SubscriberNotificationCooldownEntity : ITableEntity
    {
        public SubscriberNotificationCooldownEntity() { }

        public SubscriberNotificationCooldownEntity(string location)
        {
            PartitionKey = "SubscriberNotificationCooldown";
            RowKey = location.ToLowerInvariant();
            ETag = ETag.All;
        }

        public string PartitionKey { get; set; }
        public string RowKey { get; set; }
        public DateTimeOffset? Timestamp { get; set; }
        public ETag ETag { get; set; }
        public DateTimeOffset LastSentAt { get; set; }
    }
}
