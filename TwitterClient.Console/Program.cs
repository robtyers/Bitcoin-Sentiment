using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using Microsoft.Extensions.Configuration;
using TwitterClient.Entities;
using static TwitterClient.Entities.TwitterData;

namespace TwitterClient.Console
{
    class Program
    {
        static void Main(string[] args)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json");

            var configuration = builder.Build();

            //Configure Twitter OAuth
            var oauthToken = configuration["oauth_token"];
            var oauthTokenSecret = configuration["oauth_token_secret"];
            var oauthCustomerKey = configuration["oauth_consumer_key"];
            var oauthConsumerSecret = configuration["oauth_consumer_secret"];
            var searchGroups = configuration["twitter_keywords"];
            var removeAllUndefined = !string.IsNullOrWhiteSpace(configuration["clear_all_with_undefined_sentiment"]) && Convert.ToBoolean(configuration["clear_all_with_undefined_sentiment"]);
            var sendExtendedInformation = !string.IsNullOrWhiteSpace(configuration["send_extended_information"]) && Convert.ToBoolean(configuration["send_extended_information"]);
            var mode = configuration["match_mode"];

            var keywords = searchGroups.Contains("|") ? string.Join(",", searchGroups.Split('|')) : searchGroups;
            var tweet = new Tweet();

            var datum = tweet.StreamStatuses(new TwitterConfig(oauthToken, oauthTokenSecret, oauthCustomerKey, oauthConsumerSecret, keywords, searchGroups))
                .Where(e => !string.IsNullOrWhiteSpace(e.Text))
                .Select(t => Sentiment140.ComputeScore(t, searchGroups, mode)).Select(t =>
                    new Payload
                    {
                        CreatedAt = t.CreatedAt,
                        Topic = t.Topic,
                        SentimentScore = t.SentimentScore,
                        Author = t.UserName,
                        Text = t.Text,
                        SendExtended = sendExtendedInformation
                    });

            if (removeAllUndefined)
            {
                datum = datum.Where(e => e.SentimentScore > -1);
            }

            var tweetObserver = new TweetObserver();
            datum.Where(e => e.Topic != "No Match").ToObservable().Subscribe(tweetObserver);
        }
    }
}
