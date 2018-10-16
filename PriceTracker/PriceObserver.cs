using System;
using System.Threading;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;
using Newtonsoft.Json;

namespace PriceTracker
{
    public class PriceObserver : IObserver<CoinbaseData>
    {
        ILogger _logger;
        HubConnection _hubConnection;
        CancellationToken _cancellationToken { get; }

        public PriceObserver(ILogger logger, HubConnection hubConnection, CancellationToken cancellationToken)
        {
            _cancellationToken = cancellationToken;
            _logger = logger;
            _hubConnection = hubConnection;
        }

        void IObserver<CoinbaseData>.OnNext(CoinbaseData priceData)
        {
            try
            {
                var price = new SentimentTicker.Models.Price 
                { 
                    Timestamp = DateTime.Parse(priceData.Time.UpdatedIso), 
                    Value = double.Parse(priceData.Bpi.Gbp.Rate)             
                };

                if (_logger.IsEnabled(LogLevel.Trace))
                {
                    var message = JsonConvert.SerializeObject(price, Formatting.Indented);
                    Console.WriteLine("Broadcasting Price to Clients ({0})", message);
                }

                _hubConnection.InvokeAsync("Broadcast", "price", price, _cancellationToken);
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
    }
}
