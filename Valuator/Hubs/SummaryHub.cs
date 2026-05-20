using Microsoft.AspNetCore.SignalR;
using StackExchange.Redis;

namespace Valuator.Hubs;

public sealed class SummaryHub : Hub
{
    private readonly IDatabase _db;

    public SummaryHub( IConnectionMultiplexer redis )
    {
        _db = redis.GetDatabase();
    }

    public static string GetGroupName( string textId ) => $"summary-{textId}";

    public async Task Subscribe( string textId )
    {
        if ( string.IsNullOrWhiteSpace( textId ) )
        {
            return;
        }

        await Groups.AddToGroupAsync( Context.ConnectionId, GetGroupName( textId ) );

        RedisValue rankValue = await _db.StringGetAsync( "RANK-" + textId );
        if ( rankValue.HasValue )
        {
            await Clients.Caller.SendAsync(
                "RankCalculated",
                new
                {
                    textId,
                    rank = ( double )rankValue
                }
            );
        }
    }
}