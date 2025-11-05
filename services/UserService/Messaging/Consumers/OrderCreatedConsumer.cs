
using Confluent.Kafka;
using Contracts.Events;
using Contracts.Messaging.Configuration;
using Microsoft.Extensions.Options;
using System.Text.Json;

namespace UserService.Messaging.Consumers
{
    public class OrderCreatedConsumer : BackgroundService
    {
        private readonly KafkaOptions _options;
        private readonly ILogger<OrderCreatedConsumer> _logger;
        private IConsumer<Ignore, string> _consumer;

        public OrderCreatedConsumer(IOptions<KafkaOptions> options, ILogger<OrderCreatedConsumer> logger)
        {
            _options = options.Value;
            _logger = logger;
        }

        public override Task StartAsync(CancellationToken cancellationToken)
        {
            var config = new ConsumerConfig
            {
                BootstrapServers = _options.BootstrapServers,
                GroupId = _options.ConsumerGroup,
                AutoOffsetReset = AutoOffsetReset.Earliest,
                EnableAutoCommit = true
            };

            _consumer = new ConsumerBuilder<Ignore, string>(config).SetErrorHandler((_, e) => _logger.LogError("Kafka error: {Reason}", e.Reason)).Build(); ;
            _consumer.Subscribe(_options.Topics.Orders);

            _logger.LogInformation("OrderCreatedConsumer subscribed to {Topic}", _options.Topics.Orders);

            return base.StartAsync(cancellationToken);
        }

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            var consumeTimeout = TimeSpan.FromSeconds(1);

            while (!stoppingToken.IsCancellationRequested)
            {
                ConsumeResult<Ignore, string>? cr = null;

                try
                {
                    cr = _consumer.Consume(consumeTimeout);
                }
                catch (OperationCanceledException ex)
                {
                    _logger.LogWarning(ex, "OrderCreatedConsumer consume error");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "OrderCreatedConsumer consume error");
                    await Task.Delay(1000, stoppingToken);
                }

                if (cr == null)
                {
                    // No message available; yield so Kestrel/Swagger can start
                    await Task.Delay(100, stoppingToken);
                    continue;
                }

                try
                {
                    var evt = JsonSerializer.Deserialize<OrderCreated>(cr.Message.Value);
                    if (evt != null)
                    {
                        _logger.LogInformation("Consumed OrderCreated: OrderId={OrderId} UserId={UserId} Amount={Amount}",
                            evt.OrderId, evt.UserId, evt.TotalAmount);
                        // Note: if you want, update a local read model here.
                    }
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "Failed to process message at {TopicPartitionOffset}",
                        cr.TopicPartitionOffset);
                    // brief backoff to avoid tight error loop
                    await Task.Delay(200, stoppingToken);
                }
            }
        }

        public override Task StopAsync(CancellationToken cancellationToken)
        {
            _consumer?.Close();
            _consumer?.Dispose();
            _logger.LogInformation("Kafka consumer stopped.");
            return base.StopAsync(cancellationToken);
        }
    }
}
