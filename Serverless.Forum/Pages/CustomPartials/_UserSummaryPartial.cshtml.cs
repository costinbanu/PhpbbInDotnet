using Microsoft.AspNetCore.Mvc.RazorPages;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb.Entities;
using Serverless.Forum.Utilities;
using System.Collections.Generic;

namespace Serverless.Forum.Pages.CustomPartials
{
    public class _UserSummaryPartialModel : PageModel
    {
        public IEnumerable<PhpbbUsers> UserList { get; }

        public IEnumerable<PhpbbGroups> GroupList { get; }

        public IEnumerable<PhpbbRanks> RankList { get; }

        public _PaginationControlsPartialModel UpperPagination { get; }

        public _PaginationControlsPartialModel LowerPagination { get; }

        public string DateFormat { get; }

        public _UserSummaryPartialModel (IEnumerable<PhpbbUsers> userList, IEnumerable<PhpbbGroups> groupList, IEnumerable<PhpbbRanks> rankList, string dateFormat, Paginator paginator, string backLink, string forwardLink)
        {
            UserList = userList;
            DateFormat = dateFormat;
            GroupList = groupList;
            RankList = rankList;
            UpperPagination = new _PaginationControlsPartialModel(paginator: paginator, allowPaginationChange: false, back: backLink, forward: forwardLink, includeEasyNavigation: false);
            LowerPagination = new _PaginationControlsPartialModel(paginator: paginator, allowPaginationChange: false, back: backLink, forward: forwardLink, includeEasyNavigation: true);
        }
    }
}