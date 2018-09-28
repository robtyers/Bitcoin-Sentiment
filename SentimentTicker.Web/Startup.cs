// Licensed under the MIT license. See LICENSE file in the project root for full license information.

using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using SentimentTicker.Web.Hubs;

namespace SentimentTicker.Web
{
    public class Startup
    {
        public void ConfigureServices(IServiceCollection services)
        {
            services.AddSignalR();
            services.AddCors(o =>
            {
                o.AddPolicy("Everything", p =>
                {
                    p.AllowAnyHeader()
                        .AllowAnyMethod()
                        .AllowAnyOrigin();
                });
            });
        }

        // This method gets called by the runtime. Use this method to configure the HTTP request pipeline.
        public void Configure(IApplicationBuilder app)
        {
            app.UseFileServer();
            app.UseCors("Everything");
            app.UseSignalR(routes =>
            {
                routes.MapHub<PriceHub>("price");
                routes.MapHub<SentimentHub>("sentiment");
            });
        }
    }
}