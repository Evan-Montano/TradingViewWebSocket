using System;
using System.Collections.Generic;
using System.Linq;
using System.Text;
using System.Threading.Tasks;

namespace TradingViewWebSocket
{
    public class ChartEngine
    {
        private List<DataUpdate> dataUpdateHistory;
        public ChartEngine()
        {
            dataUpdateHistory = new List<DataUpdate>();
        }

        /// <summary>
        /// Takes in the DataUpdate, then processes the data based on the process type.
        /// </summary>
        /// <param name="dataToProcess"></param>
        public void RunChartEngine(DataUpdate dataToProcess, ProcessType processType)
        {
            try
            {
                if (dataToProcess != null)
                {
                    UpdateCalculatedProperties(dataToProcess);

                    switch (processType)
                    {
                        case ProcessType.TRAINING_ONLY:
                            break;
                        case ProcessType.PREDICTION_ONLY:
                            break;
                        case ProcessType.TRAINING_AND_PREDICTION:
                            break;
                        default:
                            break;
                    }
                }
            }
            catch (Exception ex) { Console.WriteLine(ex); }
        }


        #region Calculate Properties
        /// <summary>
        /// Uses the last record of the DataUpdate history to calculate other indicators.
        /// Skip if history is empty.
        /// Add DataUpdate Object to history.
        /// </summary>
        /// <param name="dataToProcess"></param>
        private void UpdateCalculatedProperties(DataUpdate dataToProcess)
        {
            try
            {
                if (dataUpdateHistory.Count != 0)
                {
                    DataUpdate previousDU = dataUpdateHistory[dataUpdateHistory.Count - 1];

                    dataToProcess.Delta = GetDelta(dataToProcess.Close, previousDU.Close);
                    dataToProcess.PercentChange = GetPercentChange(dataToProcess.Close, previousDU.Close);
                }
            }
            catch (Exception ex) { Console.WriteLine($"{ex.Message}"); }
            finally
            {
                dataUpdateHistory.Add(dataToProcess);
            }
        }

        private string GetPercentChange(string currentClose, string previousClose)
        {
            /// Close / (Previous Close - 1)
            double val = (Double.Parse(currentClose)) / (Double.Parse(previousClose) - 1);
            return val.ToString();
        }

        private string GetDelta(string currentClose, string previousClose)
        {
            /// Close - Previous Close
            double val = (Double.Parse(currentClose)) - (Double.Parse(previousClose));
            return val.ToString();
        }
        #endregion Calculate Properties
    }
}
