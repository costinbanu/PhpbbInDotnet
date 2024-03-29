﻿using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Database.Entities;
using PhpbbInDotnet.Domain;
using System.Collections.Generic;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials
{
    public class _UserSummaryPartialModel : PageModel
    {
        public IEnumerable<PhpbbUsers> UserList { get; }

        public IEnumerable<PhpbbGroups> GroupList { get; }

        public IEnumerable<PhpbbRanks> RankList { get; }

        public string Language { get; }

        public _PaginationControlsPartialModel UpperPagination { get; }

        public _PaginationControlsPartialModel LowerPagination { get; }

        public string DateFormat { get; }

        public _UserSummaryPartialModel (IEnumerable<PhpbbUsers> userList, IEnumerable<PhpbbGroups> groupList, IEnumerable<PhpbbRanks> rankList, string dateFormat, string language, Paginator paginator, string backLink, string forwardLink)
        {
            UserList = userList;
            DateFormat = dateFormat;
            GroupList = groupList;
            RankList = rankList;
            Language = language;
            UpperPagination = new _PaginationControlsPartialModel(paginator: paginator, allowPaginationChange: false, back: backLink, forward: forwardLink, includeEasyNavigation: false, language: language);
            LowerPagination = new _PaginationControlsPartialModel(paginator: paginator, allowPaginationChange: false, back: backLink, forward: forwardLink, includeEasyNavigation: true, language: language);
        }
    }
}