using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;

namespace EventsLogger;

class Program
{
    private const string EventsExchangeName = "valuator.events";

    public static async Task Main( string[] args )
    {
        string instanceName = args.Length > 0
            ? args[ 0 ]
            : $"EventsLogger-{Environment.ProcessId}";

        Console.WriteLine( $"{instanceName} started" );

        ConnectionFactory factory = new ConnectionFactory
        {
            HostName = "localhost",
            AutomaticRecoveryEnabled = true
        };

        await using IConnection connection = await factory.CreateConnectionAsync();
        await using IChannel channel = await connection.CreateChannelAsync();

        string queueName = $"valuator.events.logger.{instanceName.ToLowerInvariant()}";

        await DeclareTopologyAsync( channel, queueName );
        string consumerTag = await RunConsumer( channel, queueName, instanceName );

        Console.WriteLine( "Press Enter to exit" );
        Console.ReadLine();

        await channel.BasicCancelAsync( consumerTag );
        Console.WriteLine( "done" );
    }

    private static async Task DeclareTopologyAsync( IChannel channel, string queueName )
    {
        await channel.ExchangeDeclareAsync(
            exchange: EventsExchangeName,
            type: ExchangeType.Fanout
        );

        await channel.QueueDeclareAsync(
            queue: queueName,
            durable: false,
            exclusive: false,
            autoDelete: true
        );

        await channel.QueueBindAsync(
            queue: queueName,
            exchange: EventsExchangeName,
            routingKey: ""
        );
    }

    private static async Task<string> RunConsumer(
        IChannel channel,
        string queueName,
        string instanceName )
    {
        AsyncEventingBasicConsumer consumer = new( channel );
        consumer.ReceivedAsync += ( _, eventArgs ) =>
            ConsumeAsync( channel, eventArgs, instanceName );

        return await channel.BasicConsumeAsync(
            queue: queueName,
            autoAck: false,
            consumer: consumer
        );
    }

    private static async Task ConsumeAsync(
        IChannel channel,
        BasicDeliverEventArgs eventArgs,
        string instanceName )
    {
        string json = Encoding.UTF8.GetString( eventArgs.Body.ToArray() );

        CalculationEventMessage? eventMessage =
            JsonSerializer.Deserialize<CalculationEventMessage>( json );

        if ( eventMessage is null )
        {
            await channel.BasicAckAsync( eventArgs.DeliveryTag, false );
            return;
        }

        Console.WriteLine( $"{instanceName} received event:" );
        Console.WriteLine( $"  EventType: {eventMessage.EventType}" );
        Console.WriteLine( $"  TextId: {eventMessage.TextId}" );

        if ( eventMessage.Rank.HasValue )
        {
            Console.WriteLine( $"  Rank: {eventMessage.Rank}" );
        }

        if ( eventMessage.Similarity.HasValue )
        {
            Console.WriteLine( $"  Similarity: {eventMessage.Similarity}" );
        }

        await channel.BasicAckAsync( eventArgs.DeliveryTag, false );
    }
}
