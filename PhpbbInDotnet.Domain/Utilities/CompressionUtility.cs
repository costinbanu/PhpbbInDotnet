using Microsoft.AspNetCore.WebUtilities;
using Newtonsoft.Json;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Domain.Utilities
{
    public static class CompressionUtility
    {
        public static async Task<byte[]> CompressObject<T>(T source)
        {
            using var content = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(source)));
            using var memory = new MemoryStream();
            using var gzip = new GZipStream(memory, CompressionMode.Compress);
            await content.CopyToAsync(gzip);
            await gzip.FlushAsync();
            return memory.ToArray();
        }

        public static async Task<T?> DecompressObject<T>(byte[]? source)
        {
            if (source?.Any() != true)
            {
                return default;
            }

            using var content = new MemoryStream();
            using var memory = new MemoryStream(source);
            using var gzip = new GZipStream(memory, CompressionMode.Decompress);
            await gzip.CopyToAsync(content);
            await content.FlushAsync();
            return JsonConvert.DeserializeObject<T>(Encoding.UTF8.GetString(content.ToArray()));
        }

        public static async Task<string> CompressAndEncode(string input)
            => WebEncoders.Base64UrlEncode(await CompressObject(input));

        public static async Task<string?> DecodeAndDecompress(string input)
            => await DecompressObject<string>(WebEncoders.Base64UrlDecode(input));
    }
}
