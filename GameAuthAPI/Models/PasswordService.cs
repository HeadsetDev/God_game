using System.Security.Cryptography;
using System.Text;

namespace GameAuthAPI.Services
{
    public class PasswordService
    {
        public string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var passwordBytes = Encoding.UTF8.GetBytes(password);
                var hashedBytes = sha256.ComputeHash(passwordBytes);
                return Convert.ToBase64String(hashedBytes);
            }
        }

        public bool CheckPassword(string storedHash, string password)
        {
            return storedHash == HashPassword(password);
        }
    }
}
