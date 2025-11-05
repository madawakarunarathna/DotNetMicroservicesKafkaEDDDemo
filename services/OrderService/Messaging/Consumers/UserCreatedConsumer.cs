
using Confluent.Kafka;
using Contracts.Events;
using Contracts.Messaging.Configuration;
using Microsoft.Extensions.Options;
using OrderService.Domain;
using OrderService.Persistence;
using System.Text.Json;

namespace OrderService.Messaging.Consumers
{
    public class UserCreatedConsumer : BackgroundService
    {
        private readonly KafkaOptions _options;
        private readonly ILogger<UserCreatedConsumer> _logger;
        private IConsumer<Ignore, string> _consumer;
        private readonly IServiceProvider _sp;

        public UserCreatedConsumer(IOptions<KafkaOptions> options, ILogger<UserCreatedConsumer> logger, IServiceProvider sp)
        {
            _options = options.Value;
            _logger = logger;
            _sp = sp;
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
            _consumer.Subscribe(_options.Topics.Users);

            _logger.LogInformation("UserCreatedConsumer subscribed to {Topic}", _options.Topics.Users);

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
                    _logger.LogWarning(ex, "UserCreatedConsumer consume error");
                    break;
                }
                catch (Exception ex)
                {
                    _logger.LogWarning(ex, "UserCreatedConsumer consume error");
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
                    var evt = JsonSerializer.Deserialize<UserCreated>(cr.Message.Value);
                    if (evt != null)
                    {
                        _logger.LogInformation("Consumed UserCreated: UserId={UserId} Email={Email}", evt.UserId, evt.Email);

                        using var scope = _sp.CreateScope();
                        var db = scope.ServiceProvider.GetRequiredService<OrderDbContext>();
                        var existing = await db.Users.FindAsync(new object[] { evt.UserId }, stoppingToken);

                        if (existing is null) db.Users.Add(new UserRef { Id = evt.UserId, Email = evt.Email });

                        await db.SaveChangesAsync(stoppingToken);
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
