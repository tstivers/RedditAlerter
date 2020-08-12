using Newtonsoft.Json;
using RedditAlerter.App.Models;
using RedditSharp;
using RedditSharp.Things;
using SendGrid;
using SendGrid.Helpers.Mail;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.RegularExpressions;
using System.Threading;
using System.Threading.Tasks;
using Twilio;
using Twilio.Exceptions;
using Twilio.Rest.Api.V2010.Account;

namespace RedditAlerter.App
{
    internal class Program
    {
        private static readonly DateTime StartTimeUtc = DateTime.UtcNow;

        private static async Task Main(string[] args)
        {
            var config = JsonConvert.DeserializeObject<AlerterConfig>(await File.ReadAllTextAsync("config.json"));

            TwilioClient.Init(config.TwilioAccountSid, config.TwilioAuthToken);

            var webAgent = new BotWebAgent(config.RedditUsername, config.RedditPassword, config.AppClientId, config.AppClientSecret, "https://localhost:8080");
            var reddit = new Reddit(webAgent, false);

            var listeners = new List<Task>();
            var cancellationToken = new CancellationToken();

            foreach (var subreddit in config.Subreddits)
            {
                var sr = await reddit.GetSubredditAsync(subreddit.Name);
                var stream = sr.GetPosts(Subreddit.Sort.New).Stream();
                stream.Subscribe(ProcessNewPost);
                listeners.Add(stream.Enumerate(cancellationToken));
                Console.WriteLine($"subscribed to /r/{subreddit.Name}");
            }

            Console.WriteLine("listening for new posts");

            await Task.WhenAll(listeners.ToArray());
        }

        private static async void ProcessNewPost(Post post)
        {
            // skip posts made before startup
            if (post.CreatedUTC < StartTimeUtc)
                return;

            // reload config so you don't have to bounce the process while tweaking match patterns
            var config = JsonConvert.DeserializeObject<AlerterConfig>(await File.ReadAllTextAsync("config.json"));

            foreach (var alert in config.Subreddits
                .First(x => post.SubredditName.Equals(x.Name, StringComparison.OrdinalIgnoreCase)).Alerts)
            {
                var match = true;
                foreach (var pattern in alert.MatchPatterns ?? new string[0])
                    match &= Regex.IsMatch(post.Title, pattern, RegexOptions.IgnoreCase);

                if (!match)
                    continue;

                foreach (var pattern in alert.ExcludePatterns ?? new string[0])
                    match &= !Regex.IsMatch(post.Title, pattern, RegexOptions.IgnoreCase);

                if (!match)
                    continue;

                // got a match
                Console.WriteLine($"{{{alert.Name}}} {post.Title} - {post.Url}");

                bool retry;
                do
                {
                    retry = false;
                    try
                    {
                        var sms = await MessageResource.CreateAsync(
                            body: $"[{post.SubredditName}] {post.Title} - {post.Url}",
                            //mediaUrl: new[] { post.Thumbnail }.ToList(),
                            from: new Twilio.Types.PhoneNumber(config.TwilioFromNumber),
                            to: new Twilio.Types.PhoneNumber(config.TwilioToNumber)
                        );
                    }
                    catch (ApiException ex)
                    {
                        if (ex.Code == 20429)
                        {
                            Thread.Sleep(TimeSpan.FromSeconds(1));
                            retry = true;
                        }
                    }
                } while (retry);

                var client = new SendGridClient(config.SendGridApiKey);

                var msg = new SendGridMessage()
                {
                    From = new EmailAddress(config.SendGridFromEmail),
                    Subject = $"[{post.SubredditName}] {post.Title}",
                    HtmlContent = $"<p><a href=\"https://reddit.com{post.Permalink}\">{post.Title}</a></p><p><a href=\"{post.Url}\">{post.Url}</a></p>"
                };

                msg.AddTo(new EmailAddress(config.SendGridToEmail));
                var response = await client.SendEmailAsync(msg);

                // notification sent
                return;
            }

            Console.WriteLine($"{{ignored}} {post.Title}");
        }
    }
}