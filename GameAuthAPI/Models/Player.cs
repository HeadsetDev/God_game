using System.Security.Cryptography;
using System.Text;

namespace GameAuthAPI.Models
{
    public class Player
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string PasswordHash { get; set; } = string.Empty;
        public int Level { get; set; }
        public int Coins { get; set; }
        public List<PlayerItem> PlayerItems { get; set; } = new();

        public Player(string name, string password)
        {
            if (string.IsNullOrWhiteSpace(name) || name.Length < 2 || !name.All(char.IsLetter))
                throw new ArgumentException("Имя должно содержать хотя бы 2 символа и состоять только из букв.", nameof(name));

            Name = name;
            PasswordHash = HashPassword(password);
        }

        public Player() { }

        private string HashPassword(string password)
        {
            using (var sha256 = SHA256.Create())
            {
                var passwordBytes = Encoding.UTF8.GetBytes(password);
                var hashedBytes = sha256.ComputeHash(passwordBytes);
                return Convert.ToBase64String(hashedBytes);
            }
        }

        public bool CheckPassword(string password)
        {
            return PasswordHash == HashPassword(password);
        }
    }
}
