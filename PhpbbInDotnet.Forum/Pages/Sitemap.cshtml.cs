using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.RazorPages;
using Microsoft.Extensions.Configuration;
using PhpbbInDotnet.Services;
using System;
using System.Collections.Generic;
using System.Threading.Tasks;
using XSitemaps;
using PhpbbInDotnet.Utilities.Extensions;
using System.Linq;
using System.IO;

namespace PhpbbInDotnet.Forum.Pages
{
    public class SitemapModel : PageModel
    {
        private readonly IForumTreeService _forumTreeService;
        private readonly IUserService _userService;
        private readonly IConfiguration _config;

        public SitemapModel(IForumTreeService forumTreeService, IUserService userService, IConfiguration config)
        {
            _forumTreeService = forumTreeService;
            _userService = userService;
            _config = config;
        }

        public async Task<IActionResult> OnGet()
        {
            var urls = new ReadOnlyMemory<SitemapUrl>(await GetUrls().ToArrayAsync());
            var siteMaps = Sitemap.Create(urls, maxUrlCount: urls.Length);
            if (siteMaps?.Length == 1)
            {
                var stream = new MemoryStream();
                await siteMaps.First().SerializeAsync(stream);
                await stream.FlushAsync();
                stream.Seek(0, SeekOrigin.Begin);
                return File(stream, "text/xml");
            }
            return Page();
        }

        private async IAsyncEnumerable<SitemapUrl> GetUrls()
        {
            var anonymous = _userService.ClaimsPrincipalToAuthenticatedUser(await _userService.GetAnonymousClaimsPrincipal());
            var tree = await _forumTreeService.GetForumTree(anonymous, forceRefresh: false, fetchUnreadData: false);

            var maxTime = DateTime.MinValue;
            foreach (var item in tree.EmptyIfNull())
            {
                if (item is null || item.IsRestricted || item.HasPassword || item.ForumId < 1)
                {
                    continue;
                }

                var curTime = item.ForumLastPostTime?.ToUtcTime();
                if (curTime > maxTime)
                {
                    maxTime = curTime.Value;
                }

                yield return new SitemapUrl(
                    location: _forumTreeService.GetAbsoluteUrlToForum(item.ForumId),
                    modifiedAt: curTime,
                    frequency: GetChangeFrequency(curTime));
            }

            yield return new SitemapUrl(
                location: _config.GetValue<string>("BaseUrl"),
                modifiedAt: maxTime,
                frequency: GetChangeFrequency(maxTime));
        }

        private ChangeFrequency GetChangeFrequency(DateTime? lastChange)
        {
            if (lastChange is null)
            {
                return ChangeFrequency.Never;
            }

            var diff = DateTime.UtcNow - lastChange.Value;
            if (diff.TotalDays < 1)
            {
                return ChangeFrequency.Hourly;
            }
            else if (diff.TotalDays < 7)
            {
                return ChangeFrequency.Daily;
            }
            else if (diff.TotalDays < 30)
            {
                return ChangeFrequency.Weekly;
            }
            else if (diff.TotalDays < 365)
            {
                return ChangeFrequency.Monthly;
            }
            else
            {
                return ChangeFrequency.Yearly;
            }
        }
    }
}
