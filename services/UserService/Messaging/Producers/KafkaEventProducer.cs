using Confluent.Kafka;
using Contracts.Messaging.Configuration;
using Contracts.Messaging.Producers;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace UserService.Messaging.Producers
{
    public class KafkaEventProducer : IEventProducer, IDisposable
    {
        private readonly ILogger<KafkaEventProducer> _logger;
        private readonly IProducer<string, string> _producer;

        public KafkaEventProducer(IOptions<KafkaOptions> options, ILogger<KafkaEventProducer> logger)
        {
            _logger = logger;

            var config = new ProducerConfig
            {
                BootstrapServers = options.Value.BootstrapServers,
                Acks = Acks.Leader,
                MessageTimeoutMs = 5000
            };

            _producer = new ProducerBuilder<string, string>(config).Build();
        }


        public async Task ProduceAsync(string topic, string key, object value, CancellationToken ct = default)
        {
            try
            {
                var payload = JsonSerializer.Serialize(value);
                var message = new Message<string, string>
                {
                    Key = key,
                    Value = payload
                };
                var report = await _producer.ProduceAsync(topic, message, ct);
                _logger.LogInformation("Produced message to topic {Topic} with key {Key}", topic, key);
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Error producing message to topic {Topic} with key {Key}", topic, key);
            }
        }


        public void Dispose() => _producer.Dispose();
    }
}
