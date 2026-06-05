using Microsoft.AspNetCore.SignalR;

namespace Valuator.Hubs;

public class RankHub : Hub
{
    public async Task Subscribe( string textId )
    {
        await Groups.AddToGroupAsync( Context.ConnectionId, textId );
    }
}