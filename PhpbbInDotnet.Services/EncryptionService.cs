using Microsoft.Extensions.Configuration;
using System;
using System.IO;
using System.Linq;
using System.Security.Cryptography;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    class EncryptionService : IEncryptionService
    {
        private readonly IConfiguration _config;

        public EncryptionService(IConfiguration config)
        {
            _config = config;
        }

        public async Task<(string encrypted, Guid iv)> EncryptAES(string plainText, byte[]? key = null)
        {
            byte[] encrypted;
            var iv = Guid.NewGuid();
            key ??= GetEncryptionKey();

            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv.ToByteArray();
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var enc = aes.CreateEncryptor(aes.Key, aes.IV);
                using var ms = new MemoryStream();
                using var cs = new CryptoStream(ms, enc, CryptoStreamMode.Write);
                using (var sw = new StreamWriter(cs))
                {
                    await sw.WriteAsync(plainText);
                }

                encrypted = ms.ToArray();
            }

            return (Convert.ToBase64String(encrypted), iv);
        }

        public async Task<string> DecryptAES(string encryptedText, Guid iv, byte[]? key = null)
        {
            string? decrypted = null;
            byte[] cipher = Convert.FromBase64String(encryptedText);
            key ??= GetEncryptionKey();

            using (var aes = Aes.Create())
            {
                aes.Key = key;
                aes.IV = iv.ToByteArray();
                aes.Mode = CipherMode.CBC;
                aes.Padding = PaddingMode.PKCS7;

                using var dec = aes.CreateDecryptor(aes.Key, aes.IV);
                using var ms = new MemoryStream(cipher);
                using var cs = new CryptoStream(ms, dec, CryptoStreamMode.Read);
                using var sr = new StreamReader(cs);
                decrypted = await sr.ReadToEndAsync();
            }

            return decrypted;
        }

        private byte[] GetEncryptionKey()
        {
            var key1 = _config.GetValue<Guid>("Encryption:Key1").ToByteArray().ToList();
            var key2 = _config.GetValue<Guid>("Encryption:Key2").ToByteArray();
            key1.AddRange(key2);
            return key1.ToArray();
        }
    }
}
