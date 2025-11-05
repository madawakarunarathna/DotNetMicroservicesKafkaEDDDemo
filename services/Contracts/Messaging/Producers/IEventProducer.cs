namespace Contracts.Messaging.Producers
{
    public interface IEventProducer
    {
        Task ProduceAsync(string topic, string key, object value, CancellationToken ct = default);
    }
}
