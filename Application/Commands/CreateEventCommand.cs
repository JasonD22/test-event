#nullable disable
using ETMP.MessageQueue;
using MassTransit;
using MediatR;

namespace Events.API.Application.Commands
{
    public class CreateEventCommand : IRequest<bool>
    {
        public string Name { get; set; }
    }

    public record SomeEvent : IntegrationEvent
    {
        public string Foobar { get;  set; }
    }

    public class CreteEventCommandHandler : IRequestHandler<CreateEventCommand, bool>
    {
        IBusControl _bus;

        public CreteEventCommandHandler(IBusControl busControl)
        {
            _bus = busControl;
        }

        public async Task<bool> Handle(CreateEventCommand request, CancellationToken cancellationToken)
        {
            await _bus.Publish<SomeEvent>(new SomeEvent
            {
                Foobar = "Hello"
            }, cancellationToken);

            throw new NotImplementedException();
        }
    }
}