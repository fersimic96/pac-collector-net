using PacCollector.Domain.Ports;

namespace PacCollector.Application.UseCases;

public sealed class HandleBeaconUseCase
{
    private readonly IEventBus _events;

    public HandleBeaconUseCase(IEventBus events) => _events = events;

    public void Execute(string ip)
        => _events.Publish(new DomainEvent.BeaconReceived(ip, DateTimeOffset.UtcNow));
}
