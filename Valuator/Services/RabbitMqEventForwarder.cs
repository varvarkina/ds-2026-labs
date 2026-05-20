using System.Text;
using System.Text.Json;
using Microsoft.AspNetCore.SignalR;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using Valuator.Hubs;

namespace Valuator.Services;

public sealed class RabbitMqEventForwarder : BackgroundService
{
    private const string EventsExchangeName = "valuator.events";

    private readonly IHubContext<SummaryHub> _hubContext;
    private readonly ILogger<RabbitMqEventForwarder> _logger;

    private IConnection? _connection;
    private IChannel? _channel;
    private string? _consumerTag;

    public RabbitMqEventForwarder(
        IHubContext<SummaryHub> hubContext,
        ILogger<RabbitMqEventForwarder> logger )
    {
        _hubContext = hubContext;
        _logger = logger;
    }

    protected override async Task ExecuteAsync( CancellationToken stoppingToken )
    {
        try
        {
            ConnectionFactory factory = new ConnectionFactory
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

            string queueName = $"valuator.events.browser.{Guid.NewGuid():N}";

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

            AsyncEventingBasicConsumer consumer = new( _channel );
            consumer.ReceivedAsync += ConsumeAsync;

            _consumerTag = await _channel.BasicConsumeAsync(
                queue: queueName,
                autoAck: false,
                consumer: consumer
            );

            await Task.Delay( Timeout.Infinite, stoppingToken );
        }
        catch ( OperationCanceledException ) when ( stoppingToken.IsCancellationRequested )
        {
        }
        catch ( Exception ex )
        {
            _logger.LogError( ex, "RabbitMQ forwarder stopped with error" );
        }
    }

    private async Task ConsumeAsync( object? sender, BasicDeliverEventArgs eventArgs )
    {
        try
        {
            string json = Encoding.UTF8.GetString( eventArgs.Body.ToArray() );

            CalculationEventMessage? eventMessage =
                JsonSerializer.Deserialize<CalculationEventMessage>( json );

            if ( eventMessage is not null &&
                eventMessage.EventType == "RankCalculated" &&
                eventMessage.Rank.HasValue )
            {
                await _hubContext.Clients
                    .Group( SummaryHub.GetGroupName( eventMessage.TextId ) )
                    .SendAsync(
                        "RankCalculated",
                        new
                        {
                            textId = eventMessage.TextId,
                            rank = eventMessage.Rank.Value
                        }
                    );
            }
        }
        catch ( Exception ex )
        {
            _logger.LogError( ex, "Failed to forward event to browser" );
        }
        finally
        {
            if ( _channel is not null )
            {
                await _channel.BasicAckAsync( eventArgs.DeliveryTag, false );
            }
        }
    }

    public override async Task StopAsync( CancellationToken cancellationToken )
    {
        try
        {
            if ( _channel is not null && _consumerTag is not null )
            {
                await _channel.BasicCancelAsync( _consumerTag );
            }
        }
        finally
        {
            await base.StopAsync( cancellationToken );
        }
    }
}