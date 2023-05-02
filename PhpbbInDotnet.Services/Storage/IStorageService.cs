using Microsoft.AspNetCore.Http;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Domain;
using System.Collections.Generic;
using System.IO;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services.Storage
{
    public interface IStorageService
    {
        Task<(List<PhpbbAttachments> SucceededUploads, List<string> FailedUploads)> BulkAddAttachments(IEnumerable<IFormFile> attachedFiles, int userId);
        (IEnumerable<string> Succeeded, IEnumerable<string> Failed) BulkDeleteAttachments(IEnumerable<string> files);
        bool DeleteAvatar(int userId, string extension);
        bool DeleteFile(string? name, bool isAvatar);
        string? DuplicateFile(PhpbbAttachments attachment, int userId);
        Task<byte[]> GetAttachmentContents(string name);
        string? GetFilePath(string name, FileType fileType);
        string? GetFileUrl(string name, FileType fileType);
        Task<bool> UploadAvatar(int userId, Stream contents, string fileName);
        Task<bool> UpsertEmoji(string name, Stream file);
        void WriteAllTextToFile(string path, string contents);
    }
}