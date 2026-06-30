using Microsoft.AspNetCore.SignalR;
using System.Threading.Tasks;

namespace GameAuthAPI.Hubs
{
    public class ChatHub : Hub
    {
        /// <summary>
        /// Отправка сообщения всем клиентам.
        /// </summary>
        public async Task SendMessage(string user, string message)
        {
            await Clients.All.SendAsync("ReceiveMessage", user, message);
        }

        /// <summary>
        /// Отправка личного сообщения.
        /// </summary>
        public async Task SendPrivateMessage(string sender, string receiver, string message)
        {
            await Clients.User(receiver).SendAsync("ReceivePrivateMessage", sender, message);
        }

        /// <summary>
        /// Отправка сообщения в гильдию.
        /// </summary>
        public async Task SendGuildMessage(int guildId, string user, string message)
        {
            await Clients.Group($"Guild_{guildId}").SendAsync("ReceiveGuildMessage", user, message);
        }

        /// <summary>
        /// Присоединение к чату гильдии.
        /// </summary>
        public async Task JoinGuildChat(int guildId)
        {
            await Groups.AddToGroupAsync(Context.ConnectionId, $"Guild_{guildId}");
        }

        /// <summary>
        /// Выход из чата гильдии.
        /// </summary>
        public async Task LeaveGuildChat(int guildId)
        {
            await Groups.RemoveFromGroupAsync(Context.ConnectionId, $"Guild_{guildId}");
        }
    }
}