using RabbitMQ.Client;
using System.Text;
using System.Text.Json;
using GameAuthAPI.Models;

namespace GameAuthAPI.Services
{
    public class RabbitMQService
    {
        private readonly IConnection _connection;
        private readonly IModel _channel;

        public RabbitMQService()
        {
            var factory = new ConnectionFactory() { HostName = "localhost" };
            _connection = factory.CreateConnection();
            _channel = _connection.CreateModel();

            // Очередь для уведомлений
            _channel.QueueDeclare(queue: "notifications", durable: false, exclusive: false, autoDelete: false, arguments: null);

            // Очередь для аукциона
            _channel.QueueDeclare(queue: "auction", durable: false, exclusive: false, autoDelete: false, arguments: null);
        }

        public void SendMessage(string queueName, string message)
        {
            var body = Encoding.UTF8.GetBytes(message);
            _channel.BasicPublish(exchange: "", routingKey: queueName, basicProperties: null, body: body);
        }

        public string ReceiveMessage(string queueName)
        {
            var result = _channel.BasicGet(queueName, autoAck: true);
            if (result == null)
            {
                return "Нет сообщений в очереди.";
            }

            var body = result.Body.ToArray();
            return Encoding.UTF8.GetString(body);
        }

        // Метод для отправки лота на аукцион
        public void PublishAuctionLot(AuctionLot lot)
        {
            var message = JsonSerializer.Serialize(lot);
            SendMessage("auction", message);
        }

        // Метод для получения лота с аукциона
        public AuctionLot? ReceiveAuctionLot()
        {
            var message = ReceiveMessage("auction");
            if (message == "Нет сообщений в очереди.")
            {
                return null;
            }

            return JsonSerializer.Deserialize<AuctionLot>(message);
        }
    }
}