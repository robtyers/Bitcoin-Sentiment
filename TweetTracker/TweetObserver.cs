
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;
using SentimentTicker.Models;
using static TweetTracker.TwitterData;

namespace TweetTracker
{
    public class TweetObserver : IObserver<Payload>
    {
        const int Period = 50;
        readonly Queue<int> _scores = new Queue<int>();
        ILogger _logger;
        HubConnection _hubConnection;
        CancellationToken _cancellationToken { get; }

        public TweetObserver(ILogger logger, HubConnection hubConnection, CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
            _logger = logger;
            _hubConnection = hubConnection;
        }

        public void OnNext(Payload twitterPayloadData)
        {
            try
            {
                Push(twitterPayloadData.SentimentScore);

                var sentiment = new Sentiment
                {
                    Timestamp = twitterPayloadData.CreatedAt,
                    Value = _scores.Average()
                };

                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    var message = JsonConvert.SerializeObject(sentiment, Formatting.Indented);
                    Console.WriteLine("Broadcasting Sentiment to Clients ({0})", message);
                }

                _hubConnection.InvokeAsync("Broadcast", "sentiment", sentiment, _cancellationToken);
            }
            catch(Exception e)
            {
                if (_logger.IsEnabled(LogLevel.Error))
                {
                    Console.WriteLine(e.Message);
                }
            }
        }

        public void OnCompleted()
        {
            throw new NotImplementedException();
        }

        public void OnError(Exception error)
        {

        }

        public void Push(int score)
        {
            if (_scores.Count == Period)
                _scores.Dequeue();

            _scores.Enqueue(score);
        }
    }
}
