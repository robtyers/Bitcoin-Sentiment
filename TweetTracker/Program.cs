using System;
using System.IO;
using System.Linq;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using static TweetTracker.TwitterData;

namespace TweetTracker
{
    /// <summary>
    /// https://bytefish.de/blog/realtime_charts_signalr_chartjs/
    /// </summary>
    class Program
    {
        static readonly ILogger _logger = CreateLogger("Program");
        static string _oauthToken;
        static string _oauthTokenSecret;
        static string _oauthCustomerKey;
        static string _oauthConsumerSecret;
        static string _searchGroups;
        static bool _removeAllUndefined;
        static bool _sendExtendedInformation;
        static string _mode;

        static void Main(string[] args)
        {
            var builder = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json");

            var configuration = builder.Build();

            //Configure Twitter OAuth
            _oauthToken = configuration["oauth_token"];
            _oauthTokenSecret = configuration["oauth_token_secret"];
            _oauthCustomerKey = configuration["oauth_consumer_key"];
            _oauthConsumerSecret = configuration["oauth_consumer_secret"];
            _searchGroups = configuration["twitter_keywords"];
            _removeAllUndefined = !string.IsNullOrWhiteSpace(configuration["clear_all_with_undefined_sentiment"]) && Convert.ToBoolean(configuration["clear_all_with_undefined_sentiment"]);
            _sendExtendedInformation = !string.IsNullOrWhiteSpace(configuration["send_extended_information"]) && Convert.ToBoolean(configuration["send_extended_information"]);
            _mode = configuration["match_mode"];

            var cancellationTokenSource = new CancellationTokenSource();

            Task.Run(() => MainAsync(cancellationTokenSource.Token).GetAwaiter().GetResult(), cancellationTokenSource.Token);

            Console.WriteLine("Press Enter to Exit ...");
            Console.ReadLine();

            cancellationTokenSource.Cancel();
        }

        static async Task MainAsync(CancellationToken cancellationToken)
        {
            var hubConnection = new HubConnectionBuilder()
                .WithUrl("http://localhost:5000/sentiment")
                .Build();

            await hubConnection.StartAsync();

            var keywords = _searchGroups.Contains("|") ? string.Join(",", _searchGroups.Split('|')) : _searchGroups;
            var tweet = new Tweet(_logger, cancellationToken);

            var datum = tweet.StreamStatuses(new TwitterConfig(_oauthToken, _oauthTokenSecret, _oauthCustomerKey, _oauthConsumerSecret, keywords, _searchGroups))
                .Where(e => !string.IsNullOrWhiteSpace(e.Text))
                .Select(t => Sentiment140.ComputeScore(t, _searchGroups, _mode)).Select(t =>
                    new Payload
                    {
                        CreatedAt = t.CreatedAt,
                        Topic = t.Topic,
                        SentimentScore = t.SentimentScore,
                        Author = t.UserName,
                        Text = t.Text,
                        SendExtended = _sendExtendedInformation
                    });

            if (_removeAllUndefined)
            {
                datum = datum.Where(e => e.SentimentScore > -1);
            }

            var tweetObserver = new TweetObserver(_logger, hubConnection, cancellationToken);
            datum.Where(e => e.Topic != "No Match").ToObservable().Subscribe(tweetObserver);

            await hubConnection.DisposeAsync();
        }

        static ILogger CreateLogger(string loggerName)
        {
            return new LoggerFactory()
                .AddConsole(LogLevel.Trace)
                .CreateLogger(loggerName);
        }
    }
}
