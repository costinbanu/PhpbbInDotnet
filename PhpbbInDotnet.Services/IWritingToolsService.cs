using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Objects;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace PhpbbInDotnet.Services
{
    public interface IWritingToolsService
    {
        string CleanBbTextForDisplay(string? text, string? uid);
        Task<(string Message, bool? IsSuccess)> DeleteOrphanedFiles();
        List<PhpbbWords> GetBannedWords();
        Task<List<PhpbbWords>> GetBannedWordsAsync();
        string GetBbCodesCacheKey(string language);
        Task<List<PhpbbSmilies>> GetLazySmilies();
        Task<List<PhpbbSmilies>> GetSmilies();
        Task<(string Message, bool? IsSuccess)> ManageBannedWords(List<PhpbbWords> words, List<int> indexesToRemove);
        Task<(string Message, bool? IsSuccess)> ManageBBCodes(List<PhpbbBbcodes> codes, List<int> indexesToRemove, List<int> indexesToDisplay);
        Task<(string Message, bool? IsSuccess)> ManageSmilies(List<UpsertSmiliesDto> dto, List<string> newOrder, List<int> codesToDelete, List<string> smileyGroupsToDelete);
        Task<string> PrepareTextForSaving(string? text);
    }
}