using StackExchange.Redis;

namespace Valuator.Services;

public class ShardConnectionManager
{
    private readonly IConnectionMultiplexer _main;
    private readonly Dictionary<string, IConnectionMultiplexer> _shards = new();

    public ShardConnectionManager()
    {
        var mainConnStr = Environment.GetEnvironmentVariable( "DB_MAIN" ) ?? "localhost:6000";
        _main = ConnectionMultiplexer.Connect( mainConnStr );

        RegisterShard( "RU", Environment.GetEnvironmentVariable( "DB_RU" ) ?? "localhost:6001" );
        RegisterShard( "EU", Environment.GetEnvironmentVariable( "DB_EU" ) ?? "localhost:6002" );
        RegisterShard( "ASIA", Environment.GetEnvironmentVariable( "DB_ASIA" ) ?? "localhost:6003" );
    }

    private void RegisterShard( string region, string connectionString )
    {
        _shards[ region ] = ConnectionMultiplexer.Connect( connectionString );
    }

    public IDatabase GetMainDatabase() => _main.GetDatabase();

    public IDatabase GetShardDatabase( string region )
    {
        if ( _shards.TryGetValue( region, out var conn ) )
            return conn.GetDatabase();
        throw new ArgumentException( $"Unknown region: {region}" );
    }
}