using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;

namespace RankCalculator;

class Program
{
    private const string TaskQueueName = "valuator.processing.rank";
    private const string EventsExchangeName = "valuator.events";

    private static readonly Dictionary<string, IConnectionMultiplexer> _shardConnections = new();

    public static async Task Main(string[] args)
    {
        try
        {
            string instanceName = args.Length > 0
                ? args[0]
                : $"RankCalculator-{Environment.ProcessId}";

            Console.WriteLine($"{instanceName} started");

            var mainConnStr = Environment.GetEnvironmentVariable( "DB_MAIN" ) ?? "localhost:6000";
            using var mainRedis = await ConnectionMultiplexer.ConnectAsync( mainConnStr );

            _shardConnections[ "RU" ] = await ConnectionMultiplexer.ConnectAsync(
                Environment.GetEnvironmentVariable( "DB_RU" ) ?? "localhost:6001" );
            _shardConnections[ "EU" ] = await ConnectionMultiplexer.ConnectAsync(
                Environment.GetEnvironmentVariable( "DB_EU" ) ?? "localhost:6002" );
            _shardConnections[ "ASIA" ] = await ConnectionMultiplexer.ConnectAsync(
                Environment.GetEnvironmentVariable( "DB_ASIA" ) ?? "localhost:6003" );

            ConnectionFactory factory = new ConnectionFactory
            {
                HostName = "localhost",
                UserName = "guest",
                Password = "guest"
            };

            await using IConnection connection = await factory.CreateConnectionAsync();

            await using IChannel taskChannel = await connection.CreateChannelAsync();
            await using IChannel eventsChannel = await connection.CreateChannelAsync();

            await DeclareTaskTopologyAsync(taskChannel);
            await DeclareEventsTopologyAsync(eventsChannel);

            string consumerTag = await RunConsumer(taskChannel, eventsChannel, mainRedis, instanceName);

            Console.WriteLine("Press Enter to exit");
            Console.ReadLine();

            await taskChannel.BasicCancelAsync(consumerTag);

            Console.WriteLine("done");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            Console.ReadLine();
        }
    }

    private static async Task<string> RunConsumer(
        IChannel taskChannel,
        IChannel eventsChannel,
        IConnectionMultiplexer mainRedis,
        string instanceName)
    {
        AsyncEventingBasicConsumer consumer = new(taskChannel);
        consumer.ReceivedAsync += (_, eventArgs) => ConsumeAsync(taskChannel, eventsChannel, mainRedis, eventArgs, instanceName);

        return await taskChannel.BasicConsumeAsync(
            queue: TaskQueueName,
            autoAck: false,
            consumer: consumer
        );
    }

    private static async Task ConsumeAsync(
        IChannel taskChannel,
        IChannel eventsChannel,
        IConnectionMultiplexer mainRedis,
        BasicDeliverEventArgs eventArgs,
        string instanceName)
    {
        string id = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
        Console.WriteLine($"{instanceName} received id={id}");

        var mainDb = mainRedis.GetDatabase();

        var regionValue = await mainDb.StringGetAsync( $"SHARD-{id}" );
        if ( !regionValue.HasValue )
        {
            Console.WriteLine( $"{instanceName} shard map not found for id={id}" );
            await taskChannel.BasicAckAsync( eventArgs.DeliveryTag, false );
            return;
        }

        string region = regionValue.ToString();
        Console.WriteLine( $"LOOKUP: {id}, {region}" );

        if ( !_shardConnections.TryGetValue( region, out var shardConnection ) )
        {
            Console.WriteLine( $"{instanceName} unknown region '{region}' for id={id}" );
            await taskChannel.BasicAckAsync( eventArgs.DeliveryTag, false );
            return;
        }

        IDatabase shardDb = shardConnection.GetDatabase();

        var textValue = await shardDb.StringGetAsync( $"TEXT-{id}" );
        if ( !textValue.HasValue )
        {
            Console.WriteLine( $"{instanceName} text not found in shard {region} for id={id}" );
            await taskChannel.BasicAckAsync( eventArgs.DeliveryTag, false );
            return;
        }

        string text = textValue.ToString();

        int nonLetterCount = text.Count(c => !char.IsLetter(c));
        double rank = (double)nonLetterCount / text.Length;

        await shardDb.StringSetAsync("RANK-" + id, rank);

        await PublishRankCalculatedAsync(eventsChannel, id, rank);

        Console.WriteLine($"{instanceName} calculated rank for id={id}: {rank}");

        await taskChannel.BasicAckAsync(eventArgs.DeliveryTag, false);
    }

    private static async Task PublishRankCalculatedAsync(
    IChannel channel,
    string textId,
    double rank)
    {
        var eventMessage = new
        {
            EventType = "RankCalculated",
            TextId = textId,
            Rank = rank
        };

        byte[] messageData = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize(eventMessage)
        );

        await channel.BasicPublishAsync(
            exchange: EventsExchangeName,
            routingKey: "",
            mandatory: false,
            body: messageData
        );
    }

    private static async Task DeclareTaskTopologyAsync(IChannel channel)
    {
        await channel.QueueDeclareAsync(
            queue: TaskQueueName,
            durable: true,
            exclusive: false,
            autoDelete: false
        );
    }

    private static async Task DeclareEventsTopologyAsync(IChannel channel)
    {
        await channel.ExchangeDeclareAsync(
            exchange: EventsExchangeName,
            type: ExchangeType.Fanout
        );
    }
}