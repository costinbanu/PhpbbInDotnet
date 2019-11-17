using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.Contracts;
using Serverless.Forum.Utilities;
using System.Threading.Tasks;

namespace Serverless.Forum.Pages
{
    [BindProperties(SupportsGet = true), ValidateAntiForgeryToken]
    public class _ForumTreePartialModel : ModelWithLoggedUser
    {
        public _ForumTreePartialModel(IConfiguration config, Utils utils) : base(config, utils) { }

        public ForumDisplay Forums { get; set; }
        public int? ForumId { get; set; }
        public int? TopicId { get; set; }

        public async Task OnGet()
        {
            Forums = await GetForumTree();
        }
    }
}