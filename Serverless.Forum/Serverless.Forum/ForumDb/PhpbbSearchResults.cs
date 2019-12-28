using System;
using System.Collections.Generic;

namespace Serverless.Forum.ForumDb
{
    public partial class PhpbbSearchResults
    {
        public string SearchKey { get; set; }
        public int SearchTime { get; set; }
        public string SearchKeywords { get; set; }
        public string SearchAuthors { get; set; }
    }
}
