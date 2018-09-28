using System;
using Newtonsoft.Json;

namespace SentimentTicker.Models
{
    public class Sentiment
    {
        [JsonProperty("timestamp")]
        public DateTime Timestamp { get; set; }

        [JsonProperty("value")]
        public double Value { get; set; }

        public override string ToString()
        {
            return string.Format("Sentiment (Timestamp = {0}, Value = {1})", Timestamp, Value);
        }
    }
}
