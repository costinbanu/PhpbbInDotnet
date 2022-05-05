using Microsoft.AspNetCore.Html;
using Microsoft.AspNetCore.Mvc.RazorPages;
using PhpbbInDotnet.Objects;
using System;
using System.Collections.Generic;

namespace PhpbbInDotnet.Forum.Pages.CustomPartials
{
    public class _TopicDisplayPartialModel : PageModel
    {
        public AuthenticatedUserExpanded CurrentUser { get; }
        public string Language { get; }
        public List<TopicGroup> TopicGroups { get; }
        public int? ForumId { get; set; }
        public bool ShowPath { get; set; }
        public bool ShowTypeName { get; set; }
        public bool AllowNewTopicCreation { get; set; }
        public TopicSelectionOptions? TopicSelectionOptions { get; set; }

        public _TopicDisplayPartialModel(AuthenticatedUserExpanded currentUser, string language, List<TopicDto> topics)
            : this(
                  currentUser, 
                  language, 
                  new List<TopicGroup>
                  {
                      new TopicGroup 
                      { 
                          TopicType = Utilities.TopicType.Normal, 
                          Topics = topics 
                      } 
                  })
        { }

        public _TopicDisplayPartialModel(AuthenticatedUserExpanded currentUser, string language, List<TopicGroup> topicGroups)
        {
            CurrentUser = currentUser;
            Language = language;
            TopicGroups = topicGroups;
        }
    }

    public class TopicSelectionOptions
    {
        public string InputName { get; }
        public string FormName { get; }
        public Func<TopicDto, string> ValueFactory { get; }
        public int[]? SelectedTopicIds { get; set; }
        public string? OnChange { get; set; }

        public TopicSelectionOptions(string inputName, string formName, Func<TopicDto, string> inputValueFactory)
        {
            InputName = inputName; 
            FormName = formName; 
            ValueFactory = inputValueFactory; 
        }
    }
}
