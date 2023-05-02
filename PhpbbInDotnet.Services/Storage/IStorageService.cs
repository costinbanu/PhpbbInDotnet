using Microsoft.AspNetCore.Http;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Domain;
using System;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services.Storage
{
	public interface IStorageService
    {
        Task<(IEnumerable<PhpbbAttachments> SucceededUploads, IEnumerable<string> FailedUploads)> BulkAddAttachments(IEnumerable<IFormFile> attachedFiles, int userId);
        Task<(IEnumerable<string> Succeeded, IEnumerable<string> Failed)> BulkDeleteAttachments(IEnumerable<string> files);
        Task<bool> DeleteAvatar(int userId, string originalFileName);
        Task<bool> DeleteAttachment(string name);
        Task<string?> DuplicateAttachment(PhpbbAttachments attachment, int userId);
        Task<Stream?> GetFileStream(string name, FileType fileType);
		string? GetEmojiRelativeUrl(string name);
        Task<bool> UploadAvatar(int userId, Stream contents, string fileName);
        Task<bool> UpsertEmoji(string name, Stream contents);
        Task WriteAllTextToFile(string path, string contents);
        Task<DateTime?> GetLastWriteTime(string path);
    }
}