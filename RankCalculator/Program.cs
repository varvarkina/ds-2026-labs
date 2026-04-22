using System.Text;
using RabbitMQ.Client;
using RabbitMQ.Client.Events;
using StackExchange.Redis;

namespace RankCalculator;

class Program
{
    private const string QueueName = "valuator.processing.rank";

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
            await using IChannel channel = await connection.CreateChannelAsync();

            using IConnectionMultiplexer redis =
                await ConnectionMultiplexer.ConnectAsync("localhost:6379");
            IDatabase db = redis.GetDatabase();

            await DeclareTopologyAsync(channel);
            string consumerTag = await RunConsumer(channel, db, instanceName);

            Console.WriteLine("Press Enter to exit");
            Console.ReadLine();

            await channel.BasicCancelAsync(consumerTag);

            Console.WriteLine("done");
        }
        catch (Exception ex)
        {
            Console.WriteLine(ex.ToString());
            Console.ReadLine();
        }
    }

    private static async Task<string> RunConsumer(
        IChannel channel,
        IDatabase db,
        string instanceName)
    {
        AsyncEventingBasicConsumer consumer = new(channel);
        consumer.ReceivedAsync += (_, eventArgs) => ConsumeAsync(channel, db, eventArgs, instanceName);

        return await channel.BasicConsumeAsync(
            queue: QueueName,
            autoAck: false,
            consumer: consumer
        );
    }

    private static async Task ConsumeAsync(
        IChannel channel,
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
            await channel.BasicAckAsync(eventArgs.DeliveryTag, false);
            return;
        }

        string text = (string)textValue!;

        int nonLetterCount = text.Count(c => !char.IsLetter(c));
        double rank = (double)nonLetterCount / text.Length;

        await db.StringSetAsync("RANK-" + id, rank);

        Console.WriteLine($"{instanceName} calculated rank for id={id}: {rank}");

        await channel.BasicAckAsync(eventArgs.DeliveryTag, false);
    }

    private static async Task DeclareTopologyAsync(IChannel channel)
    {
        await channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false
        );
    }
}