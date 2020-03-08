﻿using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Serverless.Forum.Contracts;
using Serverless.Forum.ForumDb;
using Serverless.Forum.Services;
using Serverless.Forum.Utilities;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using System.Web;

namespace Serverless.Forum.Pages
{
    public class ViewTopicModel : ModelWithPagination
    {
        public List<PostDisplay> Posts { get; private set; }
        public string TopicTitle { get; private set; }
        public string ForumTitle { get; private set; }
        public int? ForumId { get; private set; }
        public int? PostId { get; private set; }
        public bool? Highlight { get; private set; }
        public int? TopicId => _currentTopic?.TopicId;
        public bool IsLocked => (_currentTopic?.TopicStatus ?? 0) == 1;
        public PollDisplay Poll { get; private set; }

        private PhpbbTopics _currentTopic;
        private List<PhpbbPosts> _dbPosts;
        private int? _page;
        private int? _count;
        private readonly PostService _postService;

        public ViewTopicModel(IConfiguration config, Utils utils, ForumTreeService forumService, UserService userService, CacheService cacheService, PostService postService)
            : base(config, utils, forumService, userService, cacheService)
        {
            _postService = postService;
        }

        public async Task<IActionResult> OnGetByPostId(int postId, bool? highlight)
        {
            if (_currentTopic == null)
            {
                using (var context = new ForumDbContext(_config))
                {
                    _currentTopic = await (
                        from p in context.PhpbbPosts.AsNoTracking()
                        where p.PostId == postId

                        join t in context.PhpbbTopics.AsNoTracking()
                        on p.TopicId equals t.TopicId
                        into joined

                        from j in joined
                        select j
                    ).FirstOrDefaultAsync();
                }
            }

            if (_currentTopic == null)
            {
                return NotFound($"Mesajul {postId} nu există.");
            }

            await GetPostsLazy(null, null, postId);

            PostId = postId;
            Highlight = highlight;
            return await OnGet(_currentTopic.TopicId, _page.Value);
        }

        public async Task<IActionResult> OnGet(int topicId, int pageNum)
        {
            PhpbbForums parent = null;
            using (var context = new ForumDbContext(_config))
            {
                if (_currentTopic == null)
                {
                    _currentTopic = await (from t in context.PhpbbTopics.AsNoTracking()
                                           where t.TopicId == topicId
                                           select t).FirstOrDefaultAsync();
                }

                if (_currentTopic == null)
                {
                    return NotFound($"Subiectul {topicId} nu există.");
                }

                parent = await (from f in context.PhpbbForums.AsNoTracking()

                                join t in context.PhpbbTopics.AsNoTracking()
                                on f.ForumId equals t.ForumId
                                into joined

                                from j in joined
                                where j.TopicId == topicId
                                select f).FirstOrDefaultAsync();


                ForumId = parent?.ForumId;

                var permissionError = await ValidateForumPermissionsResponsesAsync(parent, ForumId ?? 0);
                if (permissionError != null)
                {
                    return permissionError;
                }

                ForumTitle = HttpUtility.HtmlDecode(parent?.ForumName ?? "untitled");

                await GetPostsLazy(topicId, pageNum, null);
                await ComputePagination(_count.Value, pageNum, $"/ViewTopic?TopicId={topicId}&PageNum=1", topicId);


                Posts = (
                    from p in _dbPosts

                    join u in context.PhpbbUsers.AsNoTracking()
                    on p.PosterId equals u.UserId
                    into joinedUsers

                    join a in context.PhpbbAttachments.AsNoTracking()
                    on p.PostId equals a.PostMsgId
                    into joinedAttachments

                    from ju in joinedUsers.DefaultIfEmpty()

                    select new PostDisplay
                    {
                        PostSubject = p.PostSubject,
                        PostText = p.PostText,
                        AuthorName = ju == null ? "Anonymous" : (ju.UserId == 1 ? p.PostUsername : ju.Username),
                        AuthorId = ju == null ? 1 : (ju.UserId == 1 ? null as int? : ju.UserId),
                        AuthorColor = ju == null ? null : ju.UserColour,
                        PostCreationTime = p.PostTime.ToUtcTime(),
                        PostModifiedTime = p.PostEditTime.ToUtcTime(),
                        PostId = p.PostId,
                        Attachments = (from ja in joinedAttachments
                                       select ja.ToModel()).ToList(),
                        BbcodeUid = p.BbcodeUid,
                        Unread = IsPostUnread(p.TopicId, p.PostId),
                        AuthorHasAvatar = ju == null ? false : !string.IsNullOrWhiteSpace(ju.UserAvatar),
                        AuthorSignature = ju == null ? null : _postService.BbCodeToHtml(ju.UserSig, ju.UserSigBbcodeUid).RunSync()
                    }
                ).ToList();
                await _postService.ProcessPosts(Posts, PageContext, HttpContext, true);
                TopicTitle = HttpUtility.HtmlDecode(_currentTopic.TopicTitle ?? "untitled");

                await GetPoll(context);

                if (Posts.Any(p => p.Unread))
                {
                    await context.PhpbbTopicsTrack.AddAsync(new PhpbbTopicsTrack
                    {
                        ForumId = ForumId.Value,
                        MarkTime = DateTime.UtcNow.ToUnixTimestamp(),
                        TopicId = TopicId.Value,
                        UserId = CurrentUserId
                    });
                    await context.SaveChangesAsync();
                }
            }
            return Page();
        }

        public async Task<IActionResult> OnPost(int topicId, int userPostsPerPage, int postId)
        {
            async Task save(ForumDbContext localContext)
            {
                await localContext.SaveChangesAsync();
                await ReloadCurrentUser();
            }

            using (var context = new ForumDbContext(_config))
            {
                var curValue = await context.PhpbbUserTopicPostNumber.FirstOrDefaultAsync(ppp => ppp.UserId == CurrentUserId && ppp.TopicId == topicId);

                if (curValue == null)
                {
                    context.PhpbbUserTopicPostNumber.Add(
                        new PhpbbUserTopicPostNumber
                        {
                            UserId = CurrentUserId,
                            TopicId = topicId,
                            PostNo = userPostsPerPage
                        }
                    );
                    await save(context);
                }
                else if (curValue.PostNo != userPostsPerPage)
                {
                    curValue.PostNo = userPostsPerPage;
                    await save(context);
                }
                return RedirectToPage("ViewTopic", "ByPostId", new { postId = postId, highlight = false });
            }
        }


        private async Task GetPostsLazy(int? topicId, int? page, int? postId)
        {
            if (_dbPosts == null || _page == null || _count == null)
            {
                var results = await _postService.GetPostPageAsync(CurrentUserId, topicId, page, postId);
                _dbPosts = results.Posts;
                _page = results.Page;
                _count = results.Count;
            }
        }

        private async Task GetPoll(ForumDbContext context)
        {
            var dbPollOptions = await context.PhpbbPollOptions.AsNoTracking().Where(o => o.TopicId == (TopicId ?? 0)).ToListAsync();

            if (!dbPollOptions.Any() && string.IsNullOrWhiteSpace(_currentTopic.PollTitle) && _currentTopic.PollStart == 0)
            {
                return;
            }

            Poll = new PollDisplay
            {
                PollTitle = _currentTopic.PollTitle,
                PollStart = _currentTopic.PollStart.ToUtcTime(),
                PollDurationSecons = _currentTopic.PollLength,
                PollMaxOptions = _currentTopic.PollMaxOptions,
                TopicId = TopicId.Value,
                VoteCanBeChanged = _currentTopic.PollVoteChange == 1,
                PollOptions = (
                    from o in dbPollOptions
                    select new PollOption
                    {
                        PollOptionId = o.PollOptionId,
                        PollOptionText = o.PollOptionText,
                        TopicId = o.TopicId,
                        PollOptionVoters = (
                            from v in context.PhpbbPollVotes.AsNoTracking()
                            where o.PollOptionId == v.PollOptionId
                               && o.TopicId == v.TopicId

                            join u in context.PhpbbUsers.AsNoTracking()
                            on v.VoteUserId equals u.UserId
                            into joinedUsers

                            from ju in joinedUsers.DefaultIfEmpty()
                            select new PollOptionVoter
                            {
                                UserId = v.VoteUserId,
                                Username = ju == null ? "[deleted user]" : ju.Username
                            }
                        ).ToList()
                    }
                ).ToList()
            };
        }
    }
}