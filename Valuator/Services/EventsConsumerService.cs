using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Valuator.Hubs;

namespace Valuator.Services;

public class EventsConsumerService : BackgroundService
{
    private const string EventsExchangeName = "valuator.events";
    private readonly IHubContext<RankHub> _hubContext;
    private readonly ILogger<EventsConsumerService> _logger;
    private IConnection? _connection;
    private IChannel? _channel;

    public EventsConsumerService( IHubContext<RankHub> hubContext, ILogger<EventsConsumerService> logger )
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync( CancellationToken stoppingToken )
    {
        var factory = new ConnectionFactory
        {
            HostName = "localhost",
            AutomaticRecoveryEnabled = true
        };

        _connection = await factory.CreateConnectionAsync();
        _channel = await _connection.CreateChannelAsync();

        await _channel.ExchangeDeclareAsync(
            exchange: EventsExchangeName,
            type: ExchangeType.Fanout
        );

        var queueName = $"valuator.events.signalr.{Guid.NewGuid()}";
        await _channel.QueueDeclareAsync(
            queue: queueName,
            durable: false,
            exclusive: true,
            autoDelete: true
        );
        await _channel.QueueBindAsync(
            queue: queueName,
            exchange: EventsExchangeName,
            routingKey: ""
        );

        var consumer = new AsyncEventingBasicConsumer( _channel );
        consumer.ReceivedAsync += async ( _, ea ) =>
        {
            var json = Encoding.UTF8.GetString( ea.Body.ToArray() );
            var eventMessage = JsonSerializer.Deserialize<CalculationEventMessage>( json );

            if ( eventMessage?.EventType == "RankCalculated" && eventMessage.Rank.HasValue )
            {
                _logger.LogInformation(
                    "Forwarding RankCalculated for TextId={TextId} to SignalR group",
                    eventMessage.TextId );

                await _hubContext.Clients
                    .Group( eventMessage.TextId )
                    .SendAsync( "RankCalculated", new
                    {
                        textId = eventMessage.TextId,
                        rank = eventMessage.Rank.Value
                    } );
            }

            await _channel.BasicAckAsync( ea.DeliveryTag, false );
        };

        await _channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer
        );

        await Task.Delay( Timeout.Infinite, stoppingToken );
    }

    public override async Task StopAsync( CancellationToken cancellationToken )
    {
        _channel?.CloseAsync().Wait();
        _connection?.CloseAsync().Wait();
        await base.StopAsync( cancellationToken );
    }

    private sealed class CalculationEventMessage
    {
        public string EventType { get; set; } = "";
        public string TextId { get; set; } = "";
        public double? Rank { get; set; }
        public int? Similarity { get; set; }
    }
}