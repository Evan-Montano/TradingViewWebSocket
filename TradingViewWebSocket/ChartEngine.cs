using System.Globalization;
using System.Text;

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

        public void Init(ProcessType processType, string binPath = null, string idxPath = null)
        {
            try
            {
                this.CHART_BIN_PATH = binPath;
                this.CHART_IDX_PATH = idxPath;
                    
                this.binarySeeker.Init(idxPath, binPath);
                this.PROCESS_TYPE = processType;
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
                        case ProcessType.DEBUG:
                            DebugDataUpdate(dataToProcess);
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
        #endregion TRAINING_ONLY

        #region DEBUG
        private void DebugDataUpdate(DataUpdate du)
        {
            if (du == null) return;
            try
            {
                if (this.dataUpdateHistory.Count == 0)
                {
                    Console.WriteLine("DU History Empty. Continuing..");
                    this.dataUpdateHistory.Add(du);
                    return;
                }

                Console.WriteLine("Writing to binary: ");
                Console.WriteLine($"Open: {du.Open}\tHigh: {du.High}\tLow:{du.Low}\tClose: {du.Close}\tVolume: {du.Volume}\tDelta: {du.Delta}\tPercentChange: {du.PercentChange}");

                // Write to Idx/Bin, then reset state
                this.binarySeeker.CreateNewRoot(du);
                this.binarySeeker.ClearState();

                DataUpdate fromBin = new DataUpdate();
                fromBin = this.binarySeeker.GetFirstRootNode();

                Console.WriteLine($"\nRead from binary: Key: {fromBin.Key}\tFrequency: {fromBin.Frequency}\tOpen: {fromBin.Open}\tHigh: {fromBin.High}\tLow: {fromBin.Low}\tClose: {fromBin.Close}\tVolume: {fromBin.Volume}\tTopWick: {fromBin.TopWick}\tBottomWick: {fromBin.BottomWick}\tDelta: {fromBin.Delta}\tPercentChange: {fromBin.PercentChange}");


            }
            catch (Exception ex) { Console.WriteLine($"\nError whie debugging: {ex.Message}"); }
        }
        #endregion DEBUG

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
                out var inVolume, out var inDelta, out var inPctChange, out var inTopWick, out var inBottomWick,

                out var nOpen, out var nHigh, out var nLow, out var nClose,
                out var nVolume, out var nDelta, out var nPctChange, out var nTopWick, out var nBottomWick);

            if (!parsed)
                return false;

            // compute normalized distances
            double priceOpenDiff = NormalizePriceDifference(inOpen, nOpen);
            double priceHighDiff = NormalizePriceDifference(inHigh, nHigh);
            double priceLowDiff = NormalizePriceDifference(inLow, nLow);
            double priceCloseDiff = NormalizePriceDifference(inClose, nClose);

            double volDiff = NormalizeVolumeDifference(inVolume, nVolume);
            
            double deltaDiff = NormalizeRatioDifference(inDelta, nDelta);
            double pctDiff = NormalizeRatioDifference(inPctChange, nPctChange);
            if (this.dataUpdateHistory.Count < 1)
            {
                deltaDiff = 0.3;
                pctDiff = 0.3;
            }

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

            const double overallThreshold = 0.89;
            const double psycheOverrideThreshold = 0.85;

            double totalScore = priceWeight * priceShapeScore + psychWeight * psychologyScore;

            // if the psyche signals are really strong, trust them even if prices diverge
            if (psychologyScore >= psycheOverrideThreshold)
            {
                matchPercentage = (float)(psychologyScore * 100);
                return true;
            }

            // otherwise fall back to the blended threshold
            matchPercentage = (float)(totalScore * 100);
            return totalScore >= overallThreshold;

        }

        // helper to parse everything in one go
        private bool TryParseAll(DataUpdate a, DataUpdate b,
            out double aOpen, out double aHigh, out double aLow, out double aClose,
            out double aVolume, out double aDelta, out double aPctChange, out double aTopWick, out double aBottomWick,

            out double bOpen, out double bHigh, out double bLow, out double bClose,
            out double bVolume, out double bDelta, out double bPctChange, out double bTopWick, out double bBottomWick)
        {
            aOpen = aHigh = aLow = aClose = aVolume = aDelta = aPctChange = aTopWick = aBottomWick = 0;
            bOpen = bHigh = bLow = bClose = bVolume = bDelta = bPctChange = bTopWick = bBottomWick = 0;
            try
            {
                aOpen = double.Parse(a.Open);
                aHigh = double.Parse(a.High);
                aLow = double.Parse(a.Low);
                aClose = double.Parse(a.Close);
                aVolume = double.Parse(a.Volume);
                aTopWick = double.Parse(a.TopWick);
                aBottomWick = double.Parse(a.BottomWick);

                bOpen = double.Parse(b.Open);
                bHigh = double.Parse(b.High);
                bLow = double.Parse(b.Low);
                bClose = double.Parse(b.Close);
                bVolume = double.Parse(b.Volume);
                bTopWick = double.Parse(b.TopWick);
                bBottomWick = double.Parse(b.BottomWick);

                if (this.dataUpdateHistory.Count > 1)
                {
                    aDelta = double.Parse(a.Delta);
                    aPctChange = double.Parse(a.PercentChange);

                    bDelta = double.Parse(b.Delta);
                    bPctChange = double.Parse(b.PercentChange);
                }
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

        public void Dispose()
        {
            binarySeeker?.Dispose();
        }
    }

    class BinarySeeker : IDisposable
    {
        internal struct Index
        {
            internal string primaryKey;
            internal long position;
            internal string parentKey;
        }

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
            try
            {
                this._dataFileStream = new FileStream(dataFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);
                this._indexFileStream = new FileStream(indexFilePath, FileMode.Open, FileAccess.ReadWrite, FileShare.ReadWrite);

                this._dataWriter = new BinaryWriter(this._dataFileStream);
                this._dataReader = new BinaryReader(this._dataFileStream);

                this._indexWriter = new BinaryWriter(this._indexFileStream);
                this._indexReader = new BinaryReader(this._indexFileStream);
            }
            catch (Exception ex) { Console.Error.WriteLine(ex.Message); }
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

        public DataUpdate GetFirstRootNode()
        {
            this.ClearState();
            ReadIndexNode(out Index inx);

            // TODO: Check the index to make sure that it's a root, then assign the dataUpdate
            return GetDataUpdateNodeFromIndex(inx);
        }

        private void ReadIndexNode(out Index inx)
        {
            inx = default;
            try
            {
                const int KeySize = 16;

                // Read key
                string key = ReadFixedString(_indexReader, KeySize);

                // Read the 8-byute position (offset into .bin)
                // Ensure there are enough bytes remaining in the stream for an Int64
                long position = _indexReader.ReadInt64();

                // Read the 16-byte parentKey (may all be zeros/padding for root)
                string parentKey = ReadFixedString(_indexReader, KeySize);

                // Assign the out param
                inx = new Index
                {
                    primaryKey = key,
                    position = position,
                    parentKey = parentKey,
                };
            }
            catch (Exception ex) { Console.Error.WriteLine($"{ex.Message}"); }
        }

        private DataUpdate GetDataUpdateNodeFromIndex(Index inx)
        {
            DataUpdate ret = null;
            try
            {
                ret = new DataUpdate();

                // Ensure the position is valid
                if (inx.position < 0 || inx.position >= _dataFileStream.Length)
                {
                    Console.Error.WriteLine($"GetDataUpdateNodeFromIndex: invalid position {inx.position}");
                    return null;
                }

                // Position the fileStream at the position of the new node.
                _dataFileStream.Seek(inx.position, SeekOrigin.Begin);

                // Read key (16 bytes)
                const int KeySize = 16;
                string key = ReadFixedString(_dataReader, KeySize);
                ret.Key = key;

                // Read frequency (4 bytes int)
                int frequency = _dataReader.ReadInt32();
                ret.Frequency = frequency;

                // Read the doubles in the exact order they were written
                double open = _dataReader.ReadDouble();
                double high = _dataReader.ReadDouble();
                double low = _dataReader.ReadDouble();
                double close = _dataReader.ReadDouble();
                double volume = _dataReader.ReadDouble();
                double topWick = _dataReader.ReadDouble();
                double bottomWick = _dataReader.ReadDouble();
                double delta = _dataReader.ReadDouble();
                double percentage = _dataReader.ReadDouble();

                // Assign to DataUpdate. Use invariant culture when converting to string so writes/reads are stable.
                // Adjust these assignments if your DataUpdate fields are typed differently.
                ret.Open = open.ToString(CultureInfo.InvariantCulture);
                ret.High = high.ToString(CultureInfo.InvariantCulture);
                ret.Low = low.ToString(CultureInfo.InvariantCulture);
                ret.Close = close.ToString(CultureInfo.InvariantCulture);
                ret.Volume = volume.ToString(CultureInfo.InvariantCulture);
                ret.TopWick = topWick.ToString(CultureInfo.InvariantCulture);
                ret.BottomWick = bottomWick.ToString(CultureInfo.InvariantCulture);
                ret.Delta = delta.ToString(CultureInfo.InvariantCulture);
                ret.PercentChange = percentage.ToString(CultureInfo.InvariantCulture);
            }
            catch (Exception ex) { Console.Error.WriteLine(ex.ToString()); }
            return ret;
        }

        public void GetNextRootNode()
        {

        }

        public void CreateNewRoot(DataUpdate dataUpdate)
        {
            this._dataFileStream.Seek(0, SeekOrigin.End);
            this._indexFileStream.Seek(0, SeekOrigin.End);

            WriteNewNode(dataUpdate);
        }

        /// <summary>
        /// Writes either a new root or child node to idx/bin files.
        /// </summary>
        /// <param name="dataUpdate">The node to write</param>
        /// <param name="parentKeyRef">Key of the parent node, if writing a child.</param>
        /// <returns>true, if success</returns>
        private bool WriteNewNode(DataUpdate dataUpdate, string parentKeyRef = null)
        {
            bool ret = false;
            string newKey;
            string parentKey;
            long binPosition;
            try
            {
                newKey = Create16ByteKey();
                parentKey = parentKeyRef ?? string.Empty;
                binPosition = _dataFileStream.Position;


                // Write to files at the end of the file. Write empty 16Bytes to parent key if root.

                // INDEX FILE
                WriteFixedString(_indexWriter, newKey, 16); // 16 bytes string, new Key
                _indexWriter.Write(binPosition); // 8 bytes long, bin position
                WriteFixedString(_indexWriter, parentKey, 16); // 16 bytes string, parent key

                // DATA/BINARY FILE
                WriteFixedString(_dataWriter, newKey, 16); // 16 bytes string, key
                _dataWriter.Write(1); // 4 bytes int, frequency (new node gets freq of 1)

                _dataWriter.Write(Double.Parse(dataUpdate.Open)); // 8 bytes double, Candlestick Open
                _dataWriter.Write(Double.Parse(dataUpdate.High)); // 8 bytes double, Candlestick High
                _dataWriter.Write(Double.Parse(dataUpdate.Low)); // 8 bytes double, Candlestick Low
                _dataWriter.Write(Double.Parse(dataUpdate.Close)); // 8 bytes double, Candlestick Close
                _dataWriter.Write(Double.Parse(dataUpdate.Volume)); // 8 bytes double, Candlestick Volume
                _dataWriter.Write(Double.Parse(dataUpdate.TopWick)); // 8 bytes double, Candlestick topWick
                _dataWriter.Write(Double.Parse(dataUpdate.BottomWick)); // 8 bytes double, Candlestick bottomWick
                _dataWriter.Write(Double.Parse(dataUpdate.Delta)); // 8 bytes double, Candlestick delta
                _dataWriter.Write(Double.Parse(dataUpdate.PercentChange)); // 8 bytes double, Candlestick percent change

                _dataWriter.Flush();
                _indexWriter.Flush();

                ret = true;
            }
            catch (Exception e) { Console.Error.WriteLine(e.ToString()); }
            return ret;
        }

        private static void WriteFixedString(BinaryWriter writer, string value, int length)
        {
            // Ensure ASCII encoding to keep exactly 1 byte per char
            byte[] bytes = Encoding.ASCII.GetBytes(value ?? string.Empty);

            if (bytes.Length > length)
                writer.Write(bytes, 0, length); // truncate
            else
            {
                writer.Write(bytes);
                // pad remaining with nulls
                for (int i = bytes.Length; i < length; i++)
                    writer.Write((byte)0);
            }
        }

        private static string ReadFixedString(BinaryReader reader, int length)
        {
            byte[] bytes = reader.ReadBytes(length);
            if (bytes.Length == 0) return string.Empty;
            // If we got fewer bytes than requested, still decode what we have.
            // Using ASCII to ensure 1 byte == 1 char. If you used a different encoding when writing,
            // replace Encoding.ASCII with the same encoding.
            string s = Encoding.ASCII.GetString(bytes, 0, bytes.Length);
            // Trim trailing nulls and spaces that were used for padding
            return s.TrimEnd('\0', ' ');
        }

        private static string Create16ByteKey()
        {
            const int keyLength = 16;
            const string chars = "ABCDEFGHIJKLMNOPQRSTUVWXYZ0123456789";

            Random random = new Random();
            char[] buffer = new char[keyLength];

            for (int i = 0; i < keyLength; i++)
            {
                buffer[i] = chars[random.Next(chars.Length)];
            }

            return new string(buffer);
        }


    }
}
