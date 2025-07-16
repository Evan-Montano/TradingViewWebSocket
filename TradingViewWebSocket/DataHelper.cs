using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Text.Json.Nodes;
using System.Text.Json.Serialization;
using System.Threading.Tasks;

namespace TradingViewWebSocket
{
    public class DataHelper
    {
        struct DataUpdate
        {
            public string Symbol { get; set; }
            public string UnixTimestamp { get; set; }
            public string Open { get; set; }
            public string High { get; set; }
            public string Low { get; set; }
            public string Close { get; set; }
            public string Volume { get; set; }
        }
        public DataHelper() { }

        public void ProcessDataUpdate(string data, StreamWriter streamWriter)
        {
            DataUpdate dataUpdate = new DataUpdate();

            if (string.IsNullOrWhiteSpace(data))
            {
                throw new ArgumentException("Data cannot be null or empty", nameof(data));
            }
            if (streamWriter == null)
            {
                throw new ArgumentNullException(nameof(streamWriter), "StreamWriter cannot be null");
            }

            try
            {

            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error processing data: {ex.Message}");
                throw;
            }
        }

        private void ExtractCandlestickData(string data, ref DataUpdate dataUpdate)
        {
            JsonObject jsonData;
            string[] candleStickData;
            string[] dataParts = data.Split("~m~"); // This should have 4 indexes in which we extract the 1st
            if (dataParts.Length == 4)
            {
                jsonData = JsonNode.Parse(dataParts[1]) as JsonObject;

                if (jsonData != null)
                {
                    
                }
            }
        }


        private string GetDateTimeStamp(string unixTimestamp)
        {
            if (string.IsNullOrWhiteSpace(unixTimestamp) ||
                !long.TryParse(unixTimestamp, out long seconds))
            {
                throw new ArgumentException("Invalid Unix timestamp", nameof(unixTimestamp));
            }

            DateTimeOffset dto = DateTimeOffset.FromUnixTimeSeconds(seconds);
            DateTime localTime = dto.ToLocalTime().DateTime;

            return localTime.ToString("yyyy-MM-dd HH:mm:ss");
        }

        


    }
}
