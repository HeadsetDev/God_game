using BCrypt.Net;

namespace GameAuthAPI.Services
{
    public class PasswordService
    {
        // Хэширование пароля
        public string HashPassword(string password)
        {
            return BCrypt.Net.BCrypt.HashPassword(password);
        }

        // Проверка пароля
        public bool CheckPassword(string hash, string password)
        {
            return BCrypt.Net.BCrypt.Verify(password, hash);
        }
    }
}