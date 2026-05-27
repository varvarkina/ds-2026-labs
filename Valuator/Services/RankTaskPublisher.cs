using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using RabbitMQ.Client;

namespace Valuator.Services;

public sealed class RankTaskPublisher
{
    private const string ExchangeName = "valuator.processing.rank";
    private const string QueueName = "valuator.processing.rank";

    private readonly IConnection _connection;
    private readonly IChannel _channel;

    public RankTaskPublisher( IConfiguration configuration )
    {
        var rabbitConfig = configuration.GetSection( "RabbitMQ" );
        var host = rabbitConfig[ "HostName" ] ?? "localhost";
        var user = rabbitConfig[ "UserName" ] ?? "guest";
        var pass = rabbitConfig[ "Password" ] ?? "guest";

        ConnectionFactory factory = new ConnectionFactory
        {
            HostName = host,
            UserName = user,
            Password = pass,
            AutomaticRecoveryEnabled = true
        };

        _connection = factory.CreateConnectionAsync().GetAwaiter().GetResult();
        _channel = _connection.CreateChannelAsync().GetAwaiter().GetResult();

        DeclareTopologyAsync( _channel ).GetAwaiter().GetResult();
    }

    public void Publish( string id, CancellationToken ct = default )
    {
        PublishAsync( id, ct ).GetAwaiter().GetResult();
    }

    public async Task PublishAsync( string id, CancellationToken ct = default )
    {
        byte[] messageData = Encoding.UTF8.GetBytes( id );

        await _channel.BasicPublishAsync(
            exchange: ExchangeName,
            routingKey: "",
            mandatory: false,
            body: messageData,
            cancellationToken: ct
        );
    }

    private static async Task DeclareTopologyAsync( IChannel channel )
    {
        await channel.ExchangeDeclareAsync(
            exchange: ExchangeName,
            type: ExchangeType.Direct
        );

        await channel.QueueDeclareAsync(
            queue: QueueName,
            durable: true,
            exclusive: false,
            autoDelete: false
        );

        await channel.QueueBindAsync(
            queue: QueueName,
            exchange: ExchangeName,
            routingKey: ""
        );
    }
}