using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Web;
using Microsoft.Extensions.Logging;

namespace TweetTracker
{
    //********************************************************* 
    // 
    //    Copyright (c) Microsoft. All rights reserved. 
    //    This code is licensed under the Microsoft Public License. 
    //    THIS CODE IS PROVIDED *AS IS* WITHOUT WARRANTY OF 
    //    ANY KIND, EITHER EXPRESS OR IMPLIED, INCLUDING ANY 
    //    IMPLIED WARRANTIES OF FITNESS FOR A PARTICULAR 
    //    PURPOSE, MERCHANTABILITY, OR NON-INFRINGEMENT. 
    // 
    //*********************************************************
    public class TwitterData
    {
        public struct TwitterConfig
        {
            public readonly string OAuthToken;
            public readonly string OAuthTokenSecret;
            public readonly string OAuthConsumerKey;
            public readonly string OAuthConsumerSecret;
            public readonly string Keywords;
            public readonly string SearchGroups;

            public TwitterConfig(string oauthToken, string oauthTokenSecret, string oauthConsumerKey, string oauthConsumerSecret, string keywords, string searchGroups)
            {
                OAuthToken = oauthToken;
                OAuthTokenSecret = oauthTokenSecret;
                OAuthConsumerKey = oauthConsumerKey;
                OAuthConsumerSecret = oauthConsumerSecret;
                Keywords = keywords;
                SearchGroups = searchGroups;
            }
        }

        [DataContract]
        public class TwitterUser
        {
            [DataMember(Name = "time_zone")] public string TimeZone;
            [DataMember(Name = "name")] public string Name;
            [DataMember(Name = "profile_image_url")] public string ProfileImageUrl;
        }

        [DataContract]
        public class Tweet
        {
            ILogger _logger;
            CancellationToken _cancellationToken;

            [DataMember(Name = "id")] public Int64 Id;
            [DataMember(Name = "in_reply_to_status_id")] public Int64? ReplyToStatusId;
            [DataMember(Name = "in_reply_to_user_id")] public Int64? ReplyToUserId;
            [DataMember(Name = "in_reply_to_screen_name")] public string ReplyToScreenName;
            [DataMember(Name = "retweeted")] public bool Retweeted;
            [DataMember(Name = "text")] public string Text;
            [DataMember(Name = "lang")] public string Language;
            [DataMember(Name = "source")] public string Source;
            [DataMember(Name = "retweet_count")] public string RetweetCount;
            [DataMember(Name = "user")] public TwitterUser User;
            [DataMember(Name = "created_at")] public string CreatedAt;
            [IgnoreDataMember] public string RawJson;
            public Tweet(ILogger logger, CancellationToken cancellationToken)
            {
                _logger = logger;
                _cancellationToken = cancellationToken;
            }

            public IEnumerable<Tweet> StreamStatuses(TwitterConfig config)
            {
                var jsonSerializer = new DataContractJsonSerializer(typeof(Tweet));

                var streamReader = ReadTweets(config);

                while (!_cancellationToken.IsCancellationRequested)
                {
                    string line = null;
                    try { line = streamReader.ReadLine(); }
                    catch (Exception e)
                    {
                        if (_logger.IsEnabled(LogLevel.Error))
                        {
                            Console.WriteLine(e.Message);
                        }
                    }

                    if (!string.IsNullOrWhiteSpace(line) && !line.StartsWith("{\"delete\""))
                    {
                        var result = (Tweet)jsonSerializer.ReadObject(new MemoryStream(Encoding.UTF8.GetBytes(line)));
                        result.RawJson = line;
                        yield return result;
                    }

                    // Oops the Twitter has ended... or more likely some error have occurred.
                    // Reconnect to the twitter feed.
                    if (line == null)
                    {
                        streamReader = ReadTweets(config);
                    }
                }
            }
            public HttpWebRequest Request { get; set; }

            private static TextReader ReadTweets(TwitterConfig config)
            {
                const string oauthVersion = "1.0";
                const string oauthSignatureMethod = "HMAC-SHA1";

                // unique request details
                var oauthNonce = Convert.ToBase64String(new ASCIIEncoding().GetBytes(DateTime.Now.Ticks.ToString()));
                var oauthTimestamp = Convert.ToInt64(
                    (DateTime.UtcNow - new DateTime(1970, 1, 1, 0, 0, 0, 0, DateTimeKind.Utc))
                        .TotalSeconds).ToString();

                var resourceUrl = "https://stream.twitter.com/1.1/statuses/filter.json";

                // create oauth signature
                var baseString = string.Format(
                    "oauth_consumer_key={0}&oauth_nonce={1}&oauth_signature_method={2}&" +
                    "oauth_timestamp={3}&oauth_token={4}&oauth_version={5}&track={6}",
                    config.OAuthConsumerKey,
                    oauthNonce,
                    oauthSignatureMethod,
                    oauthTimestamp,
                    config.OAuthToken,
                    oauthVersion,
                    Uri.EscapeDataString(config.Keywords));

                baseString = string.Concat("POST&", Uri.EscapeDataString(resourceUrl), "&", Uri.EscapeDataString(baseString));

                var compositeKey = string.Concat(Uri.EscapeDataString(config.OAuthConsumerSecret),
                    "&", Uri.EscapeDataString(config.OAuthTokenSecret));

                string oauthSignature;
                using (var hasher = new HMACSHA1(Encoding.ASCII.GetBytes(compositeKey)))
                {
                    oauthSignature = Convert.ToBase64String(
                    hasher.ComputeHash(Encoding.ASCII.GetBytes(baseString)));
                }

                // create the request header
                var authHeader = string.Format(
                    "OAuth oauth_nonce=\"{0}\", oauth_signature_method=\"{1}\", " +
                    "oauth_timestamp=\"{2}\", oauth_consumer_key=\"{3}\", " +
                    "oauth_token=\"{4}\", oauth_signature=\"{5}\", " +
                    "oauth_version=\"{6}\"",
                    Uri.EscapeDataString(oauthNonce),
                    Uri.EscapeDataString(oauthSignatureMethod),
                    Uri.EscapeDataString(oauthTimestamp),
                    Uri.EscapeDataString(config.OAuthConsumerKey),
                    Uri.EscapeDataString(config.OAuthToken),
                    Uri.EscapeDataString(oauthSignature),
                    Uri.EscapeDataString(oauthVersion)
                    );

                // make the request
                ServicePointManager.Expect100Continue = false;

                var postBody = "track=" + HttpUtility.UrlEncode(config.Keywords);
                resourceUrl += "?" + postBody;
                var request = (HttpWebRequest)WebRequest.Create(resourceUrl);
                request.Headers.Add("Authorization", authHeader);
                request.Method = "POST";
                request.ContentType = "application/x-www-form-urlencoded";
                request.PreAuthenticate = true;
                request.AllowWriteStreamBuffering = true;
                request.CachePolicy = new System.Net.Cache.RequestCachePolicy(System.Net.Cache.RequestCacheLevel.BypassCache);

                // bail out and retry after 5 seconds
                var tresponse = request.GetResponseAsync();
                if (tresponse.Wait(5000))
                    return new StreamReader(tresponse.Result.GetResponseStream());
                else
                {
                    request.Abort();
                    return StreamReader.Null;
                }
            }
        }

        public class TwitterPayload
        {
            public Int64 Id;
            public DateTime CreatedAt;
            public string UserName;
            public string TimeZone;
            public string ProfileImageUrl;
            public string Text;
            public string Language;
            public string Topic;
            public int SentimentScore;

            public string RawJson;

            public override string ToString()
            {
                return new { ID = Id, CreatedAt, UserName, TimeZone, ProfileImageUrl, Text, Language, Topic, SentimentScore }.ToString();
            }
        }

        public class Payload
        {
            public DateTime CreatedAt { get; set; }
            public string Topic { get; set; }
            public int SentimentScore { get; set; }
            public string Author { get; set; }
            public string Text { get; set; }
            public bool SendExtended { get; set; }

            public override string ToString()
            {
                return SendExtended ? new { CreatedAt, Topic, SentimentScore, Author, Text }.ToString() : new { CreatedAt, Topic, SentimentScore }.ToString();
            }
        }

        public class TwitterMin
        {
            public Int64 Id;
            public DateTime CreatedAt;
            public string UserName;
            public string Text;
            public string Topic;
            public int SentimentScore;

            public override string ToString()
            {
                return new { ID = Id, CreatedAt, UserName, Text, Topic, SentimentScore }.ToString();
            }
        }
    }
}
