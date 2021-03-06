using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading.Tasks;
using RedditSharp;
using RedditSharp.Things;
using Serilog;

namespace RSAOModerationBot.Module
{
    public class ImagePostTrackerModule : IPostMonitorModule
    {
        private readonly Reddit _reddit;
        private readonly Subreddit _subreddit;
        private readonly ILogger _logger;

        public ImagePostTrackerModule(Reddit reddit, Subreddit subreddit, ILogger logger)
        {
            _reddit = reddit;
            _subreddit = subreddit;
            _logger = logger;
        }

        public async Task ProcessNewPostsAsync(List<Post> posts)
        {
            foreach (var post in posts)
            {
                if (!IsImagePost(post)) continue;

                var user = await _reddit.GetUserAsync(post.AuthorName);

                var recentPostsByUser = await user.GetPosts(Sort.New)
                    .TakeWhile(p => p.CreatedUTC >= DateTimeOffset.UtcNow.AddDays(-7)) // In the past week 
                    .Where(p => p.Id != post.Id) // Skip the current post
                    .Where(p => p.SubredditName == _subreddit.Name) // In /r/SAO
                    .Where(p => !p.IsSelfPost) // Not a self post
                    .ToList();

                if (!recentPostsByUser.Any(IsImagePost)) continue;
                    
                // The user has posted an image post in the past week.
                _logger.Information("Reporting possible image rule infringement.");
                await HandleInfringingPostAsync(post, recentPostsByUser.First(IsImagePost));
            }
        }
        
        private static bool IsImagePost(Post post)
        {
            if (post.IsRemoved == true) return false;
            
            return Regex
                .Match(post.Url.ToString(), @"\.(jpg|jpeg|png|gif|bmp)$")
                .Success;
        }
        
        private static async Task HandleInfringingPostAsync(Post post, Post lastPost)
        {
            var timeSpanSinceLastPost = DateTimeOffset.UtcNow - lastPost.CreatedUTC;
            
            await post.ReportAsync(
                ModeratableThing.ReportType.Other,
                $"Possible image rule violation - {lastPost.Shortlink} posted {timeSpanSinceLastPost.TotalDays:0.#} days ago");
        }
    }
}