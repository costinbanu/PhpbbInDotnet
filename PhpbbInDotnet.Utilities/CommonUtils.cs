using Force.Crc32;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using Serilog;
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
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Utilities
{
    public class CommonUtils : IDisposable
    {
        private readonly IConfiguration _config;
        private readonly ICompositeViewEngine _viewEngine;
        private readonly ITempDataProvider _tempDataProvider;
        private readonly ILogger _logger;
        private readonly MD5 _md5;

        public Regex HtmlCommentRegex { get; }

        public CommonUtils(IConfiguration config, ICompositeViewEngine viewEngine, ITempDataProvider tempDataProvider, ILogger logger)
        {
            _config = config;
            _viewEngine = viewEngine;
            _tempDataProvider = tempDataProvider;
            _logger = logger;
            
            HtmlCommentRegex = new Regex("(<!--.*?-->)|(&lt;!--.*?--&gt;)", RegexOptions.Compiled | RegexOptions.Singleline, Constants.REGEX_TIMEOUT);
            _md5 = MD5.Create();
        }

        #region Compression

        public async Task<byte[]> CompressObject<T>(T source)
        {
            using var content = new MemoryStream(Encoding.UTF8.GetBytes(JsonConvert.SerializeObject(source)));
            using var memory = new MemoryStream();
            using var gzip = new GZipStream(memory, CompressionMode.Compress);
            await content.CopyToAsync(gzip);
            await gzip.FlushAsync();
            return memory.ToArray();
        }

        public async Task<T?> DecompressObject<T>(byte[]? source)
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

        public async Task<string> CompressAndEncode(string input)
            => Convert.ToBase64String(await CompressObject(input));

        public async Task<string?> DecodeAndDecompress(string input)
            => await DecompressObject<string>(Convert.FromBase64String(input));


        #endregion Compression

        #region Hashing and encryption

        public string CalculateMD5Hash(string input)
        {
            var hash = _md5.ComputeHash(Encoding.UTF8.GetBytes(input));
            var sb = new StringBuilder();
            for (int i = 0; i < hash.Length; i++)
            {
                sb.Append(hash[i].ToString("x2"));
            }
            return sb.ToString();
        }

        public long CalculateCrc32Hash(string input)
            => long.Parse(Crc32Algorithm.Compute(Encoding.UTF8.GetBytes(input.ToLower())).ToString() + input.Length.ToString());

        public async Task<(string encrypted, Guid iv)> EncryptAES(string plainText, byte[]? key = null)
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

        public async Task<string> DecryptAES(string encryptedText, Guid iv, byte[]? key = null)
        {
            string? decrypted = null;
            byte[] cipher = Convert.FromBase64String(encryptedText);
            key ??= GetEncryptionKey();

            using (AesCryptoServiceProvider aes = new())
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

        #endregion Hashing and encryption

        #region String utils

        public string CleanString(string? input)
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

        #endregion String utils

        #region HTML rendering

        public string HtmlSafeWhitespace(int count)
        {
            var options = new[] { " ", "&nbsp;" };
            var sb = new StringBuilder();
            for (int i = 0; i < count; i++)
            {
                sb.Append(options[i % 2]);
            }
            return sb.ToString();
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
                HandleError(ex, "Error encountered while rendering a razor view programatically.");
                return string.Empty;
            }
        }

        #endregion HTML rendering

        #region Error handling

        public string HandleError(Exception ex, string? message = null)
        {
            var id = Guid.NewGuid().ToString("n");
            _logger.Error(ex, "Exception id: {id}. Message: {message}", id, message);
            return id;
        }

        public string HandleErrorAsWarning(Exception ex, string? message = null)
        {
            var id = Guid.NewGuid().ToString("n");
            _logger.Warning(ex, "Exception id: {id}. Message: {message}", id, message);
            return id;
        }

        public string EnumString(Enum @enum)
            => $"{@enum.GetType().Name}.{@enum}";

        #endregion Error handling

        public async Task SendEmail(MailMessage emailMessage)
        {
            using var smtp = new SmtpClient(_config.GetValue<string>("Smtp:Host"))
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

        public List<SelectListItem> EnumToDropDownList<T>(T? selectedItem, Func<T, string>? textTransform = null, Func<T, string>? valueTransform = null, string? defaultText = null, Func<T, bool>? valueFilter = null) 
            where T : struct, Enum
        {
            textTransform ??= x => Enum.GetName(x)!;
            valueTransform ??= x => Enum.GetName(x)!;
            valueFilter ??= x => true;
            var toReturn = Enum.GetValues<T>().Where(valueFilter).Select(
                val => new SelectListItem(textTransform(val), valueTransform(val), selectedItem.HasValue && Enum.GetName(selectedItem.Value) == Enum.GetName(val))
            ).ToList();
            if (!selectedItem.HasValue && !string.IsNullOrWhiteSpace(defaultText))
            {
                toReturn.Insert(0, new SelectListItem(defaultText, "dummyValue", true, true));
            }
            return toReturn;
        }

        public string ToCamelCaseJson<T>(T @object)
            => JsonConvert.SerializeObject(
                @object,
                Formatting.None,
                new JsonSerializerSettings { ContractResolver = new CamelCasePropertyNamesContractResolver() }
            );

        public string GetPostAttachmentsCacheKey(int postId, Guid correlationId)
            => $"PostAttachments_{postId}_{correlationId}";

        public string GetAttachmentCacheKey(int attachId, Guid correlationId)
            => $"AttachmentDto_{attachId}_{correlationId}";

        public string GetAvatarCacheKey(int userId, Guid correlationId)
            => $"Avatar_{userId}_{correlationId}";

        public string GetForumLoginCacheKey(int userId, int forumId)
            => $"ForumLogin_{userId}_{forumId}";

        public void Dispose()
        {
            _md5?.Dispose();
        }
    }
}
