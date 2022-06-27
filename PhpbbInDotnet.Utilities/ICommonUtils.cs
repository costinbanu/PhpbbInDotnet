using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Text.RegularExpressions;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Utilities
{
    public interface ICommonUtils : IDisposable
    {
        Task<string> DecryptAES(string encryptedText, Guid iv, byte[]? key = null);
        Task<(string encrypted, Guid iv)> EncryptAES(string plainText, byte[]? key = null);
        string EnumString(Enum @enum);
        List<SelectListItem> EnumToDropDownList<T>(T? selectedItem, Func<T, string>? textTransform = null, Func<T, string>? valueTransform = null, string? defaultText = null, Func<T, bool>? valueFilter = null) where T : struct, Enum;
        string GetAttachmentCacheKey(int attachId, Guid correlationId);
        string GetAvatarCacheKey(int userId, Guid correlationId);
        string GetForumLoginCacheKey(int userId, int forumId);
        string GetPostAttachmentsCacheKey(int postId, Guid correlationId);
        string HandleError(Exception ex, string? message = null);
        string HandleErrorAsWarning(Exception ex, string? message = null);
        string HtmlSafeWhitespace(int count);
        string ReadableFileSize(long fileSizeInBytes);
        Task<string> RenderRazorViewToString(string viewName, object model, PageContext pageContext, HttpContext httpContext);
        Task RetryOnceAsync(Func<Task> toDo, Func<bool> evaluateSuccess, Action fix);
        Task SendEmail(string to, string subject, string body);
        string ToCamelCaseJson<T>(T @object);
    }
}