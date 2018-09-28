// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using System.Threading.Tasks;
using Microsoft.AspNetCore.SignalR;
using SentimentTicker.Models;

namespace SentimentTicker.Web.Hubs
{
    public class PriceHub : Hub
    {
        public Task Broadcast(string sender, Price price)
        {
            return Clients
                // Do not Broadcast to Caller:
                .AllExcept(new[] { Context.ConnectionId })
                // Broadcast to all connected clients:
                .InvokeAsync("Broadcast", sender, price);
        }
    }
}