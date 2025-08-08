using System.Runtime.CompilerServices;
using TradingViewWebSocket;

class Application
{

    /*
     * MSFT
     * AAPL
     * CSCO
     * COST
     * GOOGL
     */

    /// <summary>
    /// Main entry point of the application.
    /// We define the NASDAQ symbol we will be using.
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    public static async Task Main(string[] args)
    {
        const string SYMBOL = "MSFT";
        const ProcessType processType = ProcessType.DEBUG;
        
        try
        {
            //var client = new WebSocketClient(SYMBOL, processType);
            //await client.RunAsync();


            List<DataUpdate> lst = new List<DataUpdate>()
            {
                new DataUpdate { Open="140.00", High="142.00", Low="139.00", Close="141.20", Volume="500000" },
                new DataUpdate { Open="140.05", High="142.05", Low="139.05", Close="141.25", Volume="505000" },
            };

            ChartEngine engine = new ChartEngine();
            engine.Init(processType,
                "C:\\Users\\emontano\\Documents\\Dev_Sandbox\\TVWS\\TradingViewWebSocket\\TradingViewWebSocket\\ChartData\\MSFT\\MSFT.bin",
                "C:\\Users\\emontano\\Documents\\Dev_Sandbox\\TVWS\\TradingViewWebSocket\\TradingViewWebSocket\\ChartData\\MSFT\\MSFT.idx");

            foreach (DataUpdate du in lst)
            {
                engine.RunChartEngine(du);
            }

        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

}