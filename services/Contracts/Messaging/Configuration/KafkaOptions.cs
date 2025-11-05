namespace Contracts.Messaging.Configuration
{
    public sealed class KafkaTopics
    {
        public string Users { get; set; } = "users.events";
        public string Orders { get; set; } = "orders.events";
    }

    public sealed class KafkaOptions
    {
        public string BootstrapServers { get; set; } = "";
        public string ConsumerGroup { get; set; } = "";
        public KafkaTopics Topics { get; set; } = new();
    }
}
