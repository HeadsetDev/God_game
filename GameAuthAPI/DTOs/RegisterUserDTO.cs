namespace GameAuthAPI.DTOs
{
    public class RegisterUserDto
    {
        public string Username { get; set; } = string.Empty;
        public string Password { get; set; } = string.Empty;
        public string Role { get; set; } = "Player"; // Роль по умолчанию
    }
}