using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingViewWebSocket
{
    /// <summary>
    /// This class represents the incoming candlestick data
    /// marked by messages with the "du" key, or "Data Update"
    /// </summary>
    public class DataUpdate
    {
        #region Hard Data
        public string Timestamp { get; set; }
        public string Open { get; set; }
        public string High { get; set; }
        public string Low { get; set; }
        public string Close { get; set; }
        public string Volume { get; set; }
        #endregion Hard Data

        #region Calculated Data
        /// <summary>
        /// Close - Previous Close
        /// Indicates price movement
        /// </summary>
        public string Delta { get; set; }

        /// <summary>
        /// Close / (Previous Close - 1)
        /// Normalized signal of trend strength
        /// </summary>
        public string PercentChange { get; set; }

        /// <summary>
        /// High - max(Open, Close)
        /// </summary>
        public string TopWick { get; }

        /// <summary>
        /// min(Open, Close) - Low
        /// </summary>
        public string BottomWick { get; }

        /// <summary>
        /// Absolute difference between the Open and Close
        /// </summary>
        public string CandleBodySize { get; }

        /// <summary>
        /// Time since market open
        /// </summary>
        public string MinutesSinceMarketOpen { get; }
        #endregion Calculated Data


        public DataUpdate() { }


    }
}
