using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;

namespace TradingViewWebSocket
{
    /// <summary>
    /// The purpose of this class is to assist in extracting the message
    /// data that comes through the websocket as a raw string with embedded json.
    /// </summary>
    public class DataHelper
    {
        private DataUpdate dataToLog;
        public DataHelper()
        {
            dataToLog = new DataUpdate();
        }


        public void ProcessDataUpdate(string rawDataJson, StreamWriter streamWriter)
        {
            if (string.IsNullOrWhiteSpace(rawDataJson))
                throw new ArgumentException("Data cannot be null or empty", nameof(rawDataJson));
            if (streamWriter == null)
                throw new ArgumentNullException(nameof(streamWriter));

            DataUpdate dataToLog = ExtractCandlestickData(rawDataJson);
            LogCandlestickData(dataToLog, streamWriter);
        }

        /// <summary>
        /// Method to parse the raw string and get the information from the embedded json
        /// </summary>
        /// <param name="data"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
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

        /// <summary>
        /// Converting the Unix timestamp to a local datetime
        /// </summary>
        /// <param name="seconds">Unix timestamp</param>
        /// <returns>local date time object</returns>
        private static string GetDateTimeStamp(long seconds)
        {
            // convert from Unix epoch to local DateTime
            DateTimeOffset dto = DateTimeOffset.FromUnixTimeSeconds(seconds);
            DateTime localTime = dto.ToLocalTime().DateTime;
            return localTime.ToString("yyyy-MM-dd HH:mm:ss");
        }

        /// <summary>
        /// Formats and write the incoming data to a .log file
        /// </summary>
        /// <param name="data"></param>
        /// <param name="sw"></param>
        private void LogCandlestickData(DataUpdate data, StreamWriter sw)
        {
            // Check if the incoming data has passed to the next minute/timestamp
            // We only want to log the last record that comes in
            if (data.Timestamp == this.dataToLog.Timestamp)
            {
                this.dataToLog = data;
            }
            else
            {
                var sb = new StringBuilder();
                sb.Append(this.dataToLog.Timestamp).Append('\t')
                  .Append(this.dataToLog.Open).Append('\t')
                  .Append(this.dataToLog.High).Append('\t')
                  .Append(this.dataToLog.Low).Append('\t')
                  .Append(this.dataToLog.Close).Append('\t')
                  .Append(this.dataToLog.Volume);

                sw.WriteLine(sb.ToString());
                sw.Flush();

                this.dataToLog = data;
            }
                        
        }
    }
}
