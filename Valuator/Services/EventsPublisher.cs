using System.Text;
using System.Text.Json;
using RabbitMQ.Client;
using Microsoft.Extensions.Configuration;

namespace Valuator.Services;

public sealed class EventsPublisher
{
    private const string EventsExchangeName = "valuator.events";

    private readonly IConnection _connection;
    private readonly IChannel _channel;

    public EventsPublisher( IConfiguration configuration )
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

    public void PublishSimilarityCalculated( string textId, int similarity, CancellationToken ct = default )
    {
        PublishSimilarityCalculatedAsync( textId, similarity, ct ).GetAwaiter().GetResult();
    }

    public async Task PublishSimilarityCalculatedAsync( string textId, int similarity, CancellationToken ct = default )
    {
        var eventMessage = new
        {
            EventType = "SimilarityCalculated",
            TextId = textId,
            Similarity = similarity
        };

        byte[] messageData = Encoding.UTF8.GetBytes(
            JsonSerializer.Serialize( eventMessage )
        );

        await _channel.BasicPublishAsync(
            exchange: EventsExchangeName,
            routingKey: "",
            mandatory: false,
            body: messageData,
            cancellationToken: ct
        );
    }

    private static async Task DeclareTopologyAsync( IChannel channel )
    {
        await channel.ExchangeDeclareAsync(
            exchange: EventsExchangeName,
            type: ExchangeType.Fanout
        );
    }
}
