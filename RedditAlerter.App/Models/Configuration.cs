using System.Collections.Generic;

namespace RedditAlerter.App.Models
{
    public class AlertConfig
    {
        public string Name { get; set; }
        public string[] MatchPatterns { get; set; }
        public string[] ExcludePatterns { get; set; }
    }

    public class SubredditConfig
    {
        public string Name { get; set; }
        public List<AlertConfig> Alerts { get; set; }
    }

    public class AlerterConfig
    {
        public string AppClientId { get; set; }
        public string AppClientSecret { get; set; }
        public string RedditUsername { get; set; }
        public string RedditPassword { get; set; }

        public string TwilioAccountSid { get; set; }
        public string TwilioAuthToken { get; set; }
        public string TwilioFromNumber { get; set; }
        public string TwilioToNumber { get; set; }

        public string SendGridApiKey { get; set; }
        public string SendGridFromEmail { get; set; }
        public string SendGridToEmail { get; set; }

        public List<SubredditConfig> Subreddits { get; set; }
    }
}