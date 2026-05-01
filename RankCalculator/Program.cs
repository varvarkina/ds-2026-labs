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

    public static async Task Main(string[] args)
    {
        try
        {
            string instanceName = args.Length > 0
                ? args[0]
                : $"RankCalculator-{Environment.ProcessId}";

            Console.WriteLine($"{instanceName} started");

            ConnectionFactory factory = new ConnectionFactory
            {
                HostName = "localhost",
                UserName = "guest",
                Password = "guest"
            };

            await using IConnection connection = await factory.CreateConnectionAsync();

            await using IChannel taskChannel = await connection.CreateChannelAsync();
            await using IChannel eventsChannel = await connection.CreateChannelAsync();

            using IConnectionMultiplexer redis =
                await ConnectionMultiplexer.ConnectAsync("localhost:6379");
            IDatabase db = redis.GetDatabase();

            await DeclareTaskTopologyAsync(taskChannel);
            await DeclareEventsTopologyAsync(eventsChannel);

            string consumerTag = await RunConsumer(taskChannel, eventsChannel, db, instanceName);

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
        IDatabase db,
        string instanceName)
    {
        AsyncEventingBasicConsumer consumer = new(taskChannel);
        consumer.ReceivedAsync += (_, eventArgs) => ConsumeAsync(taskChannel, eventsChannel, db, eventArgs, instanceName);

        return await taskChannel.BasicConsumeAsync(
            queue: TaskQueueName,
            autoAck: false,
            consumer: consumer
        );
    }

    private static async Task ConsumeAsync(
        IChannel taskChannel,
        IChannel eventsChannel,
        IDatabase db,
        BasicDeliverEventArgs eventArgs,
        string instanceName)
    {
        string id = Encoding.UTF8.GetString(eventArgs.Body.ToArray());
        Console.WriteLine($"{instanceName} received id={id}");

        RedisValue textValue = await db.StringGetAsync("TEXT-" + id);
        if (!textValue.HasValue)
        {
            Console.WriteLine($"{instanceName} text not found for id={id}");
            await taskChannel.BasicAckAsync(eventArgs.DeliveryTag, false);
            return;
        }

        string text = (string)textValue!;

        int nonLetterCount = text.Count(c => !char.IsLetter(c));
        double rank = (double)nonLetterCount / text.Length;

        await db.StringSetAsync("RANK-" + id, rank);

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