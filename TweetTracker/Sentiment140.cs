using System;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Net;
using System.Text;
using System.Web;
using Newtonsoft.Json.Linq;
using static TweetTracker.TwitterData;

namespace TweetTracker
{
    public static class Sentiment140
    {
        public static TwitterPayload ComputeScore(Tweet tweet, string searchGroups, string mode)
        {
            return new TwitterPayload
            {
                Id = tweet.Id,
                CreatedAt = ParseTwitterDateTime(tweet.CreatedAt),
                UserName = tweet.User?.Name,
                TimeZone = tweet.User != null ? (tweet.User.TimeZone ?? "(unknown)") : "(unknown)",
                ProfileImageUrl = tweet.User != null ? (tweet.User.ProfileImageUrl ?? "(unknown)") : "(unknown)",
                Text = tweet.Text,
                Language = tweet.Language ?? "(unknown)",
                RawJson = tweet.RawJson,
                SentimentScore = (int)Analyze(tweet.Text),
                Topic = DetermineTopicEfficiently(tweet.Text, searchGroups, mode),
            };
        }

        private static DateTime ParseTwitterDateTime(string p)
        {
            if (p == null)
                return DateTime.Now;

            p = p.Replace("+0000 ", "");

            return DateTimeOffset.TryParseExact(p, "ddd MMM dd HH:mm:ss yyyy", CultureInfo.GetCultureInfo("en-us").DateTimeFormat, DateTimeStyles.AssumeUniversal, out var result) ? result.DateTime : DateTime.Now;
        }

        private enum SentimentScore
        {
            Positive = 4,
            Neutral = 2,
            Negative = 0,
            Undefined = -1
        }

        //Mark Rowe
        private static SentimentScore Analyze(string textToAnalyze)
        {
            try
            {
                var url = $"http://www.sentiment140.com/api/classify?text={HttpUtility.UrlEncode(textToAnalyze, Encoding.UTF8)}";
                var response = WebRequest.Create(url).GetResponse();

                using (var streamReader = new StreamReader(response.GetResponseStream()))
                {
                    try
                    {
                        // Read from source
                        var line = streamReader.ReadLine();

                        // Parse
                        var jObject = JObject.Parse(line);

                        var polarity = jObject.SelectToken("results", true).SelectToken("polarity", true).Value<int>();
                        if (polarity == -1)
                        {
                            return SentimentScore.Undefined;
                        }

                        return (SentimentScore)polarity;
                    }
                    catch (Exception)
                    {
                        //False Positive
                        //return SentimentScore.Neutral;
                        return SentimentScore.Undefined;
                    }
                }
            }
            catch (Exception e)
            {
                Console.WriteLine("Sentiment calculation FAILED with:/n{0}", e);
                //False Positive
                //return SentimentScore.Neutral;
                return SentimentScore.Undefined;
            }
        }

        //MWR Add multikeyword so "bob car" can be found in "the car was bobs"
        public static string DetermineTopicEfficiently(string tweetText, string searchGroups, string mode)// bool multikeyword = false, bool all = false)
        {
            var eachGroup = searchGroups.Split('|').ToList();
            foreach (var group in eachGroup)
            {
                var allKeywords = group.Split(',').ToList();
                var all = mode.ContainsIgnoreCase("all");
                //Just look for the whole keyword in the entire phrase.. WAY LESS CYCLES than each set of words. 
                var allKeywordsRemoved = allKeywords.ToList();
                foreach (var keyword in allKeywords)
                {
                    if (tweetText.ContainsIgnoreCase(keyword))
                    {
                        if (all)
                        {
                            allKeywordsRemoved.Remove(keyword);
                            if (!allKeywordsRemoved.Any())
                            {
                                return string.Join(",", allKeywords);
                            }
                        }
                        else
                        {
                            return keyword;
                        }

                    }
                }

            }

            return "No Match";
        }
    }
}
