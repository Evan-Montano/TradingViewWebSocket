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
        public string Timestamp { get; set; }
        public string Open { get; set; }
        public string High { get; set; }
        public string Low { get; set; }
        public string Close { get; set; }
        public string Volume { get; set; }
        public DataUpdate() { }


    }
}
