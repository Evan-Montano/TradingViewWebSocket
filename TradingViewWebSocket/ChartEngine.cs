namespace TradingViewWebSocket
{
    public class ChartEngine : IDisposable
    {
        private List<DataUpdate> dataUpdateHistory;
        private BinarySeeker binarySeeker;
        private string CHART_BIN_PATH;
        private string CHART_IDX_PATH;
        private ProcessType PROCESS_TYPE;

        public ChartEngine()
        {
            dataUpdateHistory = new List<DataUpdate>();
            binarySeeker = new BinarySeeker();
        }

        public void Init(ProcessType processType, string binPath, string idxPath)
        {
            try
            {
                this.CHART_BIN_PATH = binPath;
                this.CHART_IDX_PATH = idxPath;
                this.PROCESS_TYPE = processType;

                this.binarySeeker.Init(idxPath, binPath);
            }
            catch (Exception ex) { Console.WriteLine(ex.Message);  }
        }

        /// <summary>
        /// Takes in the DataUpdate, then processes the data based on the process type.
        /// </summary>
        /// <param name="dataToProcess"></param>
        public void RunChartEngine(DataUpdate dataToProcess)
        {
            try
            {
                if (dataToProcess != null)
                {

                    UpdateCalculatedProperties(dataToProcess);

                    switch (this.PROCESS_TYPE)
                    {
                        case ProcessType.TRAINING_ONLY:
                            ProcessDataForTraining(dataToProcess);
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

        #region TRAINING_ONLY
        private void ProcessDataForTraining(DataUpdate dataToUpdate)
        {
            try
            {
                // We are processing not only the incoming `dataToUpdate`, but also working with the List collection.
                // There are a couple of scenarios:
                // 1. The List is empty. If so, we are going to look for all roots. If we find a match, we will sit until another DataUpdate. If no match, we'll create a new root.
                // 2. The List is not empty. We will perform a 2-layered nested for loop to process the data to maximize routes.
                //  For each loop:
                //          First index we search all roots for a match. If not match, a new root is created
                //          Each subsequent index will be a child of the previous index. We check against all children, if any, and either determine a match, or new child node. If match, we increase the frequency of that node. We then focus to whichever node we settled on.
                // Whenever a DataUpdate node is compaired to against the existing data, we will mark it in memory as processed so we don't attempt to increase frequencies multiple times.
                // The DataUpdate collection will be given a max length of 50

                if (dataToUpdate == null || this.binarySeeker == null) return;

                this.binarySeeker.ClearState();

                if (this.dataUpdateHistory.Count == 0)
                {
                    bool matchFound = false;
                    for (this.binarySeeker.GetFirstRootNode(); this.binarySeeker.NodeFound; this.binarySeeker.GetNextRootNode())
                    {
                        matchFound = MatchMadeInHeaven(dataToUpdate, this.binarySeeker.CurrentNode, out float matchPercentage);
                        if (matchFound) break;
                    }
                    if (!matchFound)
                    {
                        // Create new root node
                        this.binarySeeker.CreateNewRoot(dataToUpdate);
                    }
                }
                else
                {
                    
                }

                dataUpdateHistory.Add(dataToUpdate);
            }
            catch (Exception ex) { Console.WriteLine($"Error during training: {ex.Message}"); }
        }

        /// <summary>
        /// Fuzzy matching of data update nodes
        /// </summary>
        /// <param name="dataToUpdate"></param>
        /// <param name="currentNode"></param>
        /// <param name="matchPercentage"></param>
        /// <returns></returns>
        private bool MatchMadeInHeaven(DataUpdate incoming, DataUpdate node, out float matchPercentage)
        {
            matchPercentage = 0f;
            if (incoming == null || node == null)
                return false;

            // parse strings into doubles
            bool parsed = TryParseAll(incoming, node,
                out var inOpen, out var inHigh, out var inLow, out var inClose,
                out var inVol, out var inDelta, out var inPct, out var inTopWick, out var inBottomWick,
                out var nOpen, out var nHigh, out var nLow, out var nClose,
                out var nVol, out var nDelta, out var nPct, out var nTopWick, out var nBottomWick);

            if (!parsed)
                return false;

            // compute normalized distances
            double priceOpenDiff = NormalizePriceDifference(inOpen, nOpen);
            double priceHighDiff = NormalizePriceDifference(inHigh, nHigh);
            double priceLowDiff = NormalizePriceDifference(inLow, nLow);
            double priceCloseDiff = NormalizePriceDifference(inClose, nClose);

            double volDiff = NormalizeVolumeDifference(inVol, nVol);
            double deltaDiff = NormalizeRatioDifference(inDelta, nDelta);
            double pctDiff = NormalizeRatioDifference(inPct, nPct);

            double topWickDiff = NormalizePriceDifference(inTopWick, nTopWick);
            double bottomWickDiff = NormalizePriceDifference(inBottomWick, nBottomWick);

            // convert distances to similarities
            double sOpen = Similarity(priceOpenDiff);
            double sHigh = Similarity(priceHighDiff);
            double sLow = Similarity(priceLowDiff);
            double sClose = Similarity(priceCloseDiff);
            double sVol = Similarity(volDiff);
            double sDelta = Similarity(deltaDiff);
            double sPct = Similarity(pctDiff);
            double sTop = Similarity(topWickDiff);
            double sBottom = Similarity(bottomWickDiff);

            // group scores
            double priceShapeScore = (sOpen + sHigh + sLow + sClose * 2) / 5;
            double psychologyScore = (sVol + sDelta + sPct + sTop + sBottom) / 5;

            // assign group weights
            const double priceWeight = 0.6;
            const double psychWeight = 0.4;

            double totalScore = priceWeight * priceShapeScore + psychWeight * psychologyScore;
            matchPercentage = (float)(totalScore * 100.0);

            // return true if above some threshold, e.g. 75%
            return totalScore >= 0.75;
        }

        // helper to parse everything in one go
        private bool TryParseAll(DataUpdate a, DataUpdate b,
            out double aOpen, out double aHigh, out double aLow, out double aClose,
            out double aVol, out double aDelta, out double aPct, out double aTopWick, out double aBottomWick,
            out double bOpen, out double bHigh, out double bLow, out double bClose,
            out double bVol, out double bDelta, out double bPct, out double bTopWick, out double bBottomWick)
        {
            aOpen = aHigh = aLow = aClose = aVol = aDelta = aPct = aTopWick = aBottomWick = 0;
            bOpen = bHigh = bLow = bClose = bVol = bDelta = bPct = bTopWick = bBottomWick = 0;
            try
            {
                aOpen = double.Parse(a.Open);
                aHigh = double.Parse(a.High);
                aLow = double.Parse(a.Low);
                aClose = double.Parse(a.Close);
                aVol = double.Parse(a.Volume);
                aDelta = double.Parse(a.Delta);
                aPct = double.Parse(a.PercentChange);
                aTopWick = double.Parse(a.TopWick);
                aBottomWick = double.Parse(a.BottomWick);

                bOpen = double.Parse(b.Open);
                bHigh = double.Parse(b.High);
                bLow = double.Parse(b.Low);
                bClose = double.Parse(b.Close);
                bVol = double.Parse(b.Volume);
                bDelta = double.Parse(b.Delta);
                bPct = double.Parse(b.PercentChange);
                bTopWick = double.Parse(b.TopWick);
                bBottomWick = double.Parse(b.BottomWick);
                return true;
            }
            catch
            {
                return false;
            }
        }

        // normalization helpers as described above
        double NormalizePriceDifference(double x, double y) => Math.Abs(x - y) / ((x + y) / 2.0);
        double NormalizeVolumeDifference(double x, double y) => Math.Abs(x - y) / Math.Max(x, y);
        double NormalizeRatioDifference(double x, double y) => Math.Abs(x - y);
        double Similarity(double normDist) => Math.Max(0, 1.0 - normDist);

        #endregion TRAINING_ONLY

        public void Dispose()
        {
            binarySeeker?.Dispose();
        }
    }

    class BinarySeeker : IDisposable
    {
        private string FILE_PATH { get; set; }
        private FileStream _dataFileStream;
        private FileStream _indexFileStream;
        private BinaryWriter _dataWriter;
        private BinaryReader _dataReader;
        private BinaryWriter _indexWriter;
        private BinaryReader _indexReader;

        public DataUpdate CurrentNode { get; set; }
        
        public BinarySeeker() { }

        public void Init(string indexFilePath, string dataFilePath)
        {
            FILE_PATH = indexFilePath;
            this._dataFileStream = new FileStream(indexFilePath, FileMode.Open, FileAccess.ReadWrite);
            this._indexFileStream = new FileStream(indexFilePath, FileMode.Open, FileAccess.ReadWrite);

            this._dataWriter = new BinaryWriter(this._dataFileStream);
            this._dataReader = new BinaryReader(this._dataFileStream);

            this._indexWriter = new BinaryWriter(this._indexFileStream);
            this._indexReader = new BinaryReader(this._indexFileStream);
        }

        public void Dispose()
        {
            _dataReader?.Dispose();
            _dataWriter?.Dispose();
            _indexWriter?.Dispose();
            _indexReader?.Dispose();
            _dataFileStream?.Dispose();
        }

        public void ClearState()
        {
            this._dataFileStream.Seek(0, SeekOrigin.Begin);
            this._indexFileStream.Seek(0, SeekOrigin.Begin);
        }

        public void GetChildNodes(string parentIndex)
        {

        }

        public bool NodeFound { get; set; }

        public void GetFirstRootNode()
        {
            this.ClearState();

        }

        public void GetNextRootNode()
        {

        }

        public void CreateNewRoot(DataUpdate dataUpdate)
        {
            this._dataFileStream.Seek(0, SeekOrigin.End);
            this._indexFileStream.Seek(0, SeekOrigin.End);


        }

        private static string Create16ByteKey()
        {
            return string.Empty;
        }

    }
}
