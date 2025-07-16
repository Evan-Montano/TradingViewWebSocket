using System;
using System.IO;
using System.Text;
using System.Text.Json.Nodes;

namespace TradingViewWebSocket
{
    public class DataHelper
    {
        public class DataUpdate
        {
            public string Timestamp { get; set; }
            public string Open { get; set; }
            public string High { get; set; }
            public string Low { get; set; }
            public string Close { get; set; }
            public string Volume { get; set; }
        }

        public DataHelper() { }

        public void ProcessDataUpdate(string data, StreamWriter streamWriter)
        {
            if (string.IsNullOrWhiteSpace(data))
                throw new ArgumentException("Data cannot be null or empty", nameof(data));
            if (streamWriter == null)
                throw new ArgumentNullException(nameof(streamWriter));

            DataUpdate dto = ExtractCandlestickData(data);
            LogCandlestickData(dto, streamWriter);
        }

        private static DataUpdate ExtractCandlestickData(string data)
        {
            var split = data.Split("~m~", StringSplitOptions.RemoveEmptyEntries);

            foreach (var segment in split)
            {
                if (!segment.TrimStart().StartsWith("{"))
                    continue;

                var json = JsonObject.Parse(segment) as JsonObject;
                if (json == null || json["m"]?.ToString() != "du")
                    continue;

                var p = json["p"] as JsonArray;
                if (p == null || p.Count != 2)
                    continue;

                var container = p[1] as JsonObject;
                if (container == null || !container.ContainsKey("sds_1"))
                    continue;

                var sds = container["sds_1"] as JsonObject;
                var sArr = sds?["s"] as JsonArray;
                if (sArr == null || sArr.Count == 0)
                    continue;

                var barObj = sArr[0] as JsonObject;
                var vArr = barObj?["v"] as JsonArray;
                if (vArr == null || vArr.Count < 6)
                    continue;

                // timestamp may be "1752672600.0"
                string rawTs = vArr[0]!.ToString();
                long seconds;
                if (rawTs.Contains('.'))
                {
                    // parse as double then cast
                    if (!double.TryParse(rawTs, out double dbl))
                        throw new ArgumentException($"Invalid unix timestamp '{rawTs}'");
                    seconds = Convert.ToInt64(Math.Truncate(dbl));
                }
                else
                {
                    if (!long.TryParse(rawTs, out seconds))
                        throw new ArgumentException($"Invalid unix timestamp '{rawTs}'");
                }

                var du = new DataUpdate
                {
                    Timestamp = GetDateTimeStamp(seconds),
                    Open = vArr[1]!.ToString(),
                    High = vArr[2]!.ToString(),
                    Low = vArr[3]!.ToString(),
                    Close = vArr[4]!.ToString(),
                    Volume = vArr[5]!.ToString()
                };

                return du;
            }

            throw new InvalidOperationException("No valid sds_1 data block found in message.");
        }

        private static string GetDateTimeStamp(long seconds)
        {
            // convert from Unix epoch to local DateTime
            DateTimeOffset dto = DateTimeOffset.FromUnixTimeSeconds(seconds);
            DateTime localTime = dto.ToLocalTime().DateTime;
            return localTime.ToString("yyyy-MM-dd HH:mm:ss");
        }

        private static void LogCandlestickData(DataUpdate data, StreamWriter sw)
        {
            var sb = new StringBuilder();
            sb.Append(data.Timestamp).Append('\t')
              .Append(data.Open).Append('\t')
              .Append(data.High).Append('\t')
              .Append(data.Low).Append('\t')
              .Append(data.Close).Append('\t')
              .Append(data.Volume);

            sw.WriteLine(sb.ToString());
            sw.Flush();
        }
    }
}
