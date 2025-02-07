namespace GameAuthAPI.Models
{
    public class RegisterUser
    {
        public string Username { get; set; } = string.Empty; // Гарантируем, что строка не будет null
        public string Password { get; set; } = string.Empty; // Аналогично для пароля
    }
}
