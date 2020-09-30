using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Forum.Contracts;
using PhpbbInDotnet.Forum.ForumDb.Entities;
using PhpbbInDotnet.Forum.Utilities;
using System.Collections.Generic;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials
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