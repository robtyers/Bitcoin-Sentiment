using System;
using System.Collections.Generic;
using System.IO;
using System.Net;
using System.Runtime.Serialization;
using System.Runtime.Serialization.Json;
using System.Text;
using System.Threading;
using Microsoft.Extensions.Logging;

namespace PriceTracker
{
    [DataContract]
    public class Time
    {
        [DataMember(Name = "updated")] public string Updated { get; set; }
        [DataMember(Name = "updatedISO")] public string UpdatedIso { get; set; }
        [DataMember(Name = "updateduk")] public string Updateduk { get; set; }
    }

    [DataContract]
    public class Price
    {
        [DataMember(Name = "code")] public string Code { get; set; }
        [DataMember(Name = "symbol")] public string Symbol { get; set; }
        [DataMember(Name = "rate")] public string Rate { get; set; }
        [DataMember(Name = "description")] public string Description { get; set; }
        [DataMember(Name = "rateFloat")] public double RateFloat { get; set; }
    }

    [DataContract]
    public class Bpi
    {
        [DataMember(Name = "USD")] public Price Usd { get; set; }
        [DataMember(Name = "GBP")] public Price Gbp { get; set; }
        [DataMember(Name = "EUR")] public Price Eur { get; set; }
    }

    [DataContract]
    public class CoinbaseData
    {
        [DataMember(Name = "time")] public Time Time { get; set; }
        [DataMember(Name = "disclaimer")] public string Disclaimer { get; set; }
        [DataMember(Name = "chartName")] public string ChartName { get; set; }
        [DataMember(Name = "bpi")] public Bpi Bpi { get; set; }

        ILogger _logger;
        CancellationToken _cancellationToken;

        public CoinbaseData(ILogger logger, CancellationToken cancellationToken)
        {
            _logger = logger;
            _cancellationToken = cancellationToken;
        }

        public IEnumerable<CoinbaseData> StreamPrices()
        {
            var jsonSerializer = new DataContractJsonSerializer(typeof(CoinbaseData));

            var streamReader = ReadPrice();
            while (!_cancellationToken.IsCancellationRequested)
            {
                string line = null;
                try
                {
                    line = streamReader.ReadLine();
                }
                catch (Exception e)
                {
                    if (_logger.IsEnabled(LogLevel.Error))
                    {
                        Console.WriteLine(e.Message);
                    }
                }

                if (!string.IsNullOrWhiteSpace(line))
                {
                    var result = (CoinbaseData)jsonSerializer.ReadObject(new MemoryStream(Encoding.UTF8.GetBytes(line)));
                    yield return result;
                }

                // Oops the Coinbase has ended... or more likely some error have occurred.
                // Reconnect to the Coinbase feed.
                if (line == null)
                {
                    streamReader = ReadPrice();
                }

                Thread.Sleep(1000);
            }
        }

        static StreamReader ReadPrice()
        {
            var resourceUrl = "https://api.coindesk.com/v1/bpi/currentprice.json";

            var request = (HttpWebRequest)WebRequest.Create(resourceUrl);
            request.Method = "GET";

            // bail out and retry after 5 seconds
            var response = request.GetResponseAsync();
            if (response.Wait(5000))
                return new StreamReader(response.Result.GetResponseStream());
            else
            {
                request.Abort();
                return StreamReader.Null;
            }
        }
    }
}
