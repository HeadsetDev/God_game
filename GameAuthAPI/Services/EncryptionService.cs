using System.Security.Cryptography;
using System.Text;

namespace GameAuthAPI.Services
{
    public class EncryptionService
    {
        // AES требует ключ ровно 16/24/32 байта. Чтобы не зависеть от длины
        // строки в конфиге, детерминированно приводим её к 32 байтам через SHA-256.
        private readonly byte[] _key;

        public EncryptionService(IConfiguration config)
        {
            var keyString = config["Encryption:Key"] ?? throw new InvalidOperationException("Encryption:Key not found");
            _key = SHA256.HashData(Encoding.UTF8.GetBytes(keyString));
        }

        public string Encrypt(string plainText)
        {
            if (string.IsNullOrEmpty(plainText))
                return plainText;

            using var aes = Aes.Create();
            aes.Key = _key;
            aes.GenerateIV(); // новый случайный IV на каждое шифрование

            using var encryptor = aes.CreateEncryptor();
            var plainBytes = Encoding.UTF8.GetBytes(plainText);
            var encryptedBytes = encryptor.TransformFinalBlock(plainBytes, 0, plainBytes.Length);

            // Храним IV вместе с шифротекстом: [16 байт IV][шифротекст]
            var result = new byte[aes.IV.Length + encryptedBytes.Length];
            Buffer.BlockCopy(aes.IV, 0, result, 0, aes.IV.Length);
            Buffer.BlockCopy(encryptedBytes, 0, result, aes.IV.Length, encryptedBytes.Length);

            return Convert.ToBase64String(result);
        }

        public string Decrypt(string cipherText)
        {
            if (string.IsNullOrEmpty(cipherText))
                return cipherText;

            var fullCipher = Convert.FromBase64String(cipherText);

            using var aes = Aes.Create();
            aes.Key = _key;

            var iv = new byte[16];
            var cipherBytes = new byte[fullCipher.Length - iv.Length];
            Buffer.BlockCopy(fullCipher, 0, iv, 0, iv.Length);
            Buffer.BlockCopy(fullCipher, iv.Length, cipherBytes, 0, cipherBytes.Length);

            aes.IV = iv;

            using var decryptor = aes.CreateDecryptor();
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