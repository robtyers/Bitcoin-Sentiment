using System;
using System.Reactive.Linq;
using System.Threading;
using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR.Client;
using Microsoft.Extensions.Logging;

namespace PriceTracker
{
    /// <summary>
    /// https://bytefish.de/blog/realtime_charts_signalr_chartjs/
    /// </summary>
    class Program
    {
        static readonly ILogger logger = CreateLogger("Program");

        static void Main(string[] args)
        {
            var cancellationTokenSource = new CancellationTokenSource();

            Task.Run(() => MainAsync(cancellationTokenSource.Token).GetAwaiter().GetResult(), cancellationTokenSource.Token);

            Console.WriteLine("Press Enter to Exit ...");
            Console.ReadLine();

            cancellationTokenSource.Cancel();
        }

        static async Task MainAsync(CancellationToken cancellationToken)
        {
            var hubConnection = new HubConnectionBuilder()
                .WithUrl("http://localhost:5000/price")
                .Build();

            await hubConnection.StartAsync();

            var coinbase = new CoinbaseData(logger, cancellationToken);
            var datum = coinbase.StreamPrices();
            var priceObserver = new PriceObserver(logger, hubConnection, cancellationToken);
            datum.ToObservable().Subscribe(priceObserver);

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
