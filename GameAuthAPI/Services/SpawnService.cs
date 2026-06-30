using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using GameAuthAPI.Models;

namespace GameAuthAPI.Services
{
    public class SpawnService
    {
        private readonly RabbitMQService _rabbitMQService;

        public SpawnService(RabbitMQService rabbitMQService)
        {
            _rabbitMQService = rabbitMQService;
        }

        public void SpawnMob(Mob mob)
        {
            var message = JsonSerializer.Serialize(mob);
            _rabbitMQService.SendMessage("mob_spawn", message);
        }

        public void SpawnNPC(NPC npc)
        {
            var message = JsonSerializer.Serialize(npc);
            _rabbitMQService.SendMessage("npc_spawn", message);
        }
    }
}