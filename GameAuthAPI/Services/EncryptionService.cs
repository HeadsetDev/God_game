using System.Security.Cryptography;
using System.Text;

namespace GameAuthAPI.Services
{
    public class EncryptionService
    {
        private readonly byte[] _key;
        private readonly byte[] _iv;

        public EncryptionService(IConfiguration config)
        {
            var keyString = config["Encryption:Key"] ?? throw new InvalidOperationException("Encryption:Key not found");
            var ivString = config["Encryption:IV"] ?? throw new InvalidOperationException("Encryption:IV not found");

            _key = Encoding.UTF8.GetBytes(keyString);
            _iv = Encoding.UTF8.GetBytes(ivString);
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;

            var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            return Convert.ToBase64String(encryptedBytes);
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return cipherText;

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.IV = _iv;

            var decryptor = aes.CreateDecryptor();
            var cipherBytes = Convert.FromBase64String(cipherText);
            var decryptedBytes = decryptor.TransformFinalBlock(cipherBytes, 0, cipherBytes.Length);

            return Encoding.UTF8.GetString(decryptedBytes);
        }

        public T DecryptObject<T>(string cipherText) where T : class
        {
            var decrypted = Decrypt(cipherText);
            return System.Text.Json.JsonSerializer.Deserialize<T>(decrypted) ?? throw new InvalidOperationException("Deserialization failed");
        }

        public string EncryptObject<T>(T obj) where T : class
        {
            var json = System.Text.Json.JsonSerializer.Serialize(obj);
            return Encrypt(json);
        }

        public bool TryDecrypt(string cipherText, out string result)
        {
            try
            {
                result = Decrypt(cipherText);
                return true;
            }
            catch
            {
                result = string.Empty;
                return false;
            }
        }
    }
}