using System;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public interface IEncryptionService
    {
        Task<string> DecryptAES(string encryptedText, Guid iv, byte[]? key = null);
        Task<(string encrypted, Guid iv)> EncryptAES(string plainText, byte[]? key = null);
    }
}