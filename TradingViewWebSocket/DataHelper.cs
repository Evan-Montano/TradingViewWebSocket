using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;

namespace TradingViewWebSocket
{
    public enum ProcessType
    {
        TRAINING_ONLY,
        PREDICTION_ONLY,
        TRAINING_AND_PREDICTION
    }


    /// <summary>
    /// The purpose of this class is to assist in extracting the message
    /// data that comes through the websocket as a raw string with embedded json.
    /// </summary>
    public class DataHelper
    {
        private DataUpdate dataToLog;
        private ChartEngine chartEngine;
        private ProcessType _processType;
        private StreamWriter logFile;

        public DataHelper()
        {
            dataToLog = new DataUpdate();
            chartEngine = new ChartEngine();
        }

        public DataHelper(ProcessType type, string CHART_SYMBOL)
        {
            this._processType = type;
            dataToLog = new DataUpdate();
            chartEngine = new ChartEngine();

            // Build the relative path
            string basePath = Path.Combine(AppContext.BaseDirectory, "..", "..", "..", "ChartData", CHART_SYMBOL);
            Directory.CreateDirectory(basePath); // Ensure directory exists

            string logPath = Path.Combine(basePath, $"{CHART_SYMBOL}.log");
            logFile = new StreamWriter(logPath, append: true);
        }

        /// <summary>
        /// Taking in the raw du information, then deciding what to do with it.
        /// </summary>
        /// <param name="rawJsonData"></param>
        /// <param name="streamWriter"></param>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="ArgumentNullException"></exception>
        public void ProcessDataUpdate(string rawJsonData, string CHART_SYMBOL)
        {
            if (string.IsNullOrWhiteSpace(rawJsonData))
                throw new ArgumentException("Data cannot be null or empty", nameof(rawJsonData));
            if (logFile == null)
                throw new ArgumentNullException(nameof(logFile));

            DataUpdate currentData = ExtractCandlestickData(rawJsonData, CHART_SYMBOL);

            // Check if the incoming data has passed to the next minute/timestamp
            // We only want to log the last record that comes in
            if (this.dataToLog.Timestamp != currentData.Timestamp)
            {
                LogCandlestickData(this.dataToLog, logFile);
                this.chartEngine.RunChartEngine(this.dataToLog, this._processType);
            }

            // Always update to the newest data, regardless of whether it's the same or a new timestamp
            this.dataToLog = currentData;
        }

        /// <summary>
        /// Method to parse the raw string and get the information from the embedded json
        /// </summary>
        /// <param name="rawJsonData"></param>
        /// <returns></returns>
        /// <exception cref="ArgumentException"></exception>
        /// <exception cref="InvalidOperationException"></exception>
        private static DataUpdate ExtractCandlestickData(string rawJsonData, string CHART_SYMBOL)
        {
            var split = rawJsonData.Split("~m~", StringSplitOptions.RemoveEmptyEntries);

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

                var du = new DataUpdate(CHART_SYMBOL)
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
        /// <param name="dataToLog"></param>
        /// <param name="sw"></param>
        private void LogCandlestickData(DataUpdate dataToLog, StreamWriter sw)
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
        }
    }
}
