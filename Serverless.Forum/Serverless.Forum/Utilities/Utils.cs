using Force.Crc32;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using Serverless.Forum.ForumDb;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Net;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Utilities
{
    public class Utils
    {
        private readonly IConfiguration _config;
        private readonly ICompositeViewEngine _viewEngine;
        private readonly ITempDataProvider _tempDataProvider;
        private readonly ILogger<Utils> _logger;

        public Utils(IConfiguration config, ICompositeViewEngine viewEngine, ITempDataProvider tempDataProvider, ILogger<Utils> logger)
        {
            _config = config;
            _viewEngine = viewEngine;
            _tempDataProvider = tempDataProvider;
            _logger = logger;
        }

        public async Task<byte[]> CompressObject<T>(T source)
        {
            using var content = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(source)));
            using var memory = new MemoryStream();
            using var gzip = new GZipStream(memory, CompressionMode.Compress);
            await content.CopyToAsync(gzip);
            await gzip.FlushAsync();
            return memory.ToArray();
        }

        public async Task<T> DecompressObject<T>(byte[] source)
        {
            if (!(source?.Any() ?? false))
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

        public async Task<string> CompressAndUrlEncode(string input)
            => HttpUtility.UrlEncode(Convert.ToBase64String(await CompressObject(input)));

        public async Task<string> UrlDecodeAndDecompress(string input)
            => await DecompressObject<string>(Convert.FromBase64String(HttpUtility.UrlDecode(input)));

        public string RandomString(int length = 8)
        {
            var chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZabcdefghijklmnopqrstuvwxyz0123456789";
            var stringChars = new char[length];
            var random = new Random();

            for (int i = 0; i < stringChars.Length; i++)
            {
                stringChars[i] = chars[random.Next(chars.Length)];
            }

            return new string(stringChars);
        }

        public string CalculateMD5Hash(string input)
        {
            var hash = MD5.Create().ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("x2"));
            }
            return sb.ToString();
        }

        public long CalculateCrc32Hash(string input)
            => long.Parse(Crc32Algorithm.Compute(Encoding.UTF8.GetBytes(input.ToLower())).ToString() + input.Length.ToString());

        public string CleanString(string input)
        {
            if (input == null)
            {
                return string.Empty;
            }

            var normalizedString = input.Normalize(NormalizationForm.FormD);
            var stringBuilder = new StringBuilder();

            foreach (var c in normalizedString)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(c);
                if (unicodeCategory != UnicodeCategory.NonSpacingMark)
                {
                    stringBuilder.Append(c);
                }
            }

            return stringBuilder.ToString().ToLower().Normalize(NormalizationForm.FormC);
        }

        public string HtmlSafeWhitespace(int count)
        {
            var options = new[] { " ", "&nbsp;" };
            var sb = new StringBuilder();
            for(int i = 0; i < count; i++)
            {
                sb.Append(options[i % 2]);
            }
            return sb.ToString();
        }

        public (string result, int offset) ReplaceAtIndex(string haystack, string needle, string replacement, int index)
        {
            if (index + needle.Length > haystack.Length || haystack.Substring(index, needle.Length) != needle)
            {
                return (haystack, 0);
            }
            return (haystack.Insert(index, replacement).Remove(index + replacement.Length, needle.Length), replacement.Length - needle.Length);
        }

        public async Task SendEmail(MailMessage emailMessage)
        {
            using var smtp = new SmtpClient(_config.GetValue<string>("Smtp:Host"), _config.GetValue<int>("Smtp:Post"))
            {
                EnableSsl = true,
                UseDefaultCredentials = false,
                Credentials = new NetworkCredential
                {
                    UserName = _config.GetValue<string>("Smtp:Username"),
                    Password = _config.GetValue<string>("Smtp:Password")
                }
            };
            await smtp.SendMailAsync(emailMessage);
        }

        public async Task<string> RenderRazorViewToString(string viewName, object model, PageContext pageContext, HttpContext httpContext)
        {
            try
            {
                var actionContext = new ActionContext(httpContext, httpContext.GetRouteData(), pageContext.ActionDescriptor);
                var viewResult = _viewEngine.FindView(actionContext, viewName, false);

                if (viewResult.View == null)
                {
                    throw new ArgumentNullException($"{viewName} does not match any available view");
                }

                var viewDictionary = new ViewDataDictionary(new EmptyModelMetadataProvider(), new ModelStateDictionary())
                {
                    Model = model
                };

                using var sw = new StringWriter();
                var viewContext = new ViewContext(
                    actionContext,
                    viewResult.View,
                    viewDictionary,
                    new TempDataDictionary(actionContext.HttpContext, _tempDataProvider),
                    sw,
                    new HtmlHelperOptions()
                )
                {
                    RouteData = httpContext.GetRouteData()
                };

                await viewResult.View.RenderAsync(viewContext);
                return sw.GetStringBuilder().ToString();
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Error encountered while rendering a razor view programatically.");
                return string.Empty;
            }
        }

        public string ReadableFileSize(long fileSizeInBytes)
        {
            var suf = new[] { "B", "KB", "MB", "GB", "TB", "PB", "EB" };
            if (fileSizeInBytes == 0)
            {
                return "0" + suf[0];
            }
            var bytes = Math.Abs(fileSizeInBytes);
            var place = Convert.ToInt32(Math.Floor(Math.Log(bytes, 1024)));
            var num = Math.Round(bytes / Math.Pow(1024, place), 2);
            return $"{(Math.Sign(fileSizeInBytes) * num).ToString("##.##", CultureInfo.InvariantCulture)} {suf[place]}";
        }

        public async Task<(string encrypted, Guid iv)> EncryptAES(string plainText, byte[] key = null)
        {
            byte[] encrypted;
            var iv = Guid.NewGuid();
            key ??= GetEncryptionKey();

            using (var aes = new AesCryptoServiceProvider())
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

        public async Task<string> DecryptAES(string encryptedText, Guid iv, byte[] key = null)
        {
            string decrypted = null;
            byte[] cipher = Convert.FromBase64String(encryptedText);
            key ??= GetEncryptionKey();

            using (AesCryptoServiceProvider aes = new AesCryptoServiceProvider())
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

        public byte[] GetEncryptionKey()
        {
            var key1 = _config.GetValue<Guid>("Encryption:Key1").ToByteArray().ToList();
            var key2 = _config.GetValue<Guid>("Encryption:Key2").ToByteArray();
            key1.AddRange(key2);
            return key1.ToArray();
        }

        public List<SelectListItem> EnumToDropDownList<T>(T? selectedItem) where T : struct, Enum
        {
            var toReturn = Enum.GetNames(typeof(T)).Select(x => new SelectListItem(x, x, selectedItem.HasValue && Enum.GetName(selectedItem.Value.GetType(), selectedItem.Value) == x)).ToList();
            if (!selectedItem.HasValue)
            {
                toReturn.Insert(0, new SelectListItem("Alege o opțiune", string.Empty, true, true));
            }
            return toReturn;
        }

        public string HandleError(Exception ex, string message = null)
        {
            var id = Guid.NewGuid().ToString("n");
            _logger.LogError(ex, $"Exception id '{id}'.\n{message}");
            return id;
        }
    }
}
