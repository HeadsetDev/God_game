using Microsoft.AspNetCore.SignalR;

namespace GameAuthAPI.Hubs
{
    public class GameHub : Hub
    {
        public async Task NotifyPlayerMovement(int playerId, string playerName, string locationName)
        {
            await Clients.All.SendAsync("ReceivePlayerMovement", playerId, playerName, locationName);
        }
    }
}