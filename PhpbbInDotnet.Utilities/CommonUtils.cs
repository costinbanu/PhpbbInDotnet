using Force.Crc32;
using MailKit.Security;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Mvc.ViewEngines;
using Microsoft.AspNetCore.Mvc.ViewFeatures;
using Microsoft.AspNetCore.Routing;
using Microsoft.Extensions.Configuration;
using MimeKit;
using MimeKit.Text;
using Newtonsoft.Json;
using Newtonsoft.Json.Serialization;
using PhpbbInDotnet.Utilities.Core;
using Serilog;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.IO.Compression;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using System.Web;

namespace PhpbbInDotnet.Utilities
{
    class CommonUtils : ICommonUtils
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

        public Task<byte[]> CompressObject<T>(T source)
            => CompressionUtils.CompressObject<T>(source);

        public Task<T?> DecompressObject<T>(byte[]? source)
            => CompressionUtils.DecompressObject<T>(source);

        public async Task<string> CompressAndEncode(string input)
            => HttpUtility.UrlEncode(Convert.ToBase64String(await CompressObject(input)));

        public async Task<string?> DecodeAndDecompress(string input)
            => await DecompressObject<string>(Convert.FromBase64String(HttpUtility.UrlDecode(input)));


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

        #endregion Hashing and encryption

        #region String utils

        public string CleanString(string? input)
            => StringUtils.CleanString(input);

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

        public async Task SendEmail(string to, string subject, string body)
        {
            using var email = new MimeMessage();
            email.From.Add(MailboxAddress.Parse(_config.GetValue<string>("AdminEmail")));
            email.To.Add(MailboxAddress.Parse(to));
            email.Subject = subject;
            email.Body = new TextPart(TextFormat.Html)
            {
                Text = body
            };
            using var smtp = new MailKit.Net.Smtp.SmtpClient();
            smtp.Connect(_config.GetValue<string>("Smtp:Host"), _config.GetValue<int>("Smtp:Port"), SecureSocketOptions.SslOnConnect);
            smtp.Authenticate(_config.GetValue<string>("Smtp:Username"), _config.GetValue<string>("Smtp:Password"));
            await smtp.SendAsync(email);
            smtp.Disconnect(true);
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

        /// <summary>
        /// If <paramref name="evaluateSuccess"/> returns false, then it retries the <paramref name="toDo"/> logic once, after applying the <paramref name="fix"/> logic.
        /// </summary>
        /// <param name="toDo">Logic to retry if failing.</param>
        /// <param name="evaluateSuccess">Logic to evaluate the success of the initial run.</param>
        /// <param name="fix">Logic to run if the initial run has failed, before retrying it.</param>
        /// <returns></returns>
        public async Task RetryOnceAsync(Func<Task> toDo, Func<bool> evaluateSuccess, Action fix)
        {
            await toDo();
            if (!evaluateSuccess())
            {
                fix();
                await toDo();
            }
        }

        public void Dispose()
        {
            _md5?.Dispose();
        }
    }
}
