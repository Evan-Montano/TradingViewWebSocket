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
        
        try
        {
            //var client = new WebSocketClient(SYMBOL);
            //await client.RunAsync();


            var lst = new List<DataUpdate>()
            {
                new DataUpdate { Open="270.00", High="273.00", Low="269.00", Close="269.50", Volume="100000" },


                new DataUpdate { Open="270.00", High="272.00", Low="269.00", Close="271.50", Volume="800000" },
                new DataUpdate { Open="270.10", High="272.10", Low="269.10", Close="271.60", Volume="810000" },

                new DataUpdate { Open="140.00", High="142.00", Low="139.00", Close="141.20", Volume="500000" },
                new DataUpdate { Open="140.05", High="142.05", Low="139.05", Close="141.25", Volume="505000" },

                new DataUpdate { Open="85.00", High="86.50", Low="84.50", Close="86.00", Volume="300000" },
                new DataUpdate { Open="85.02", High="86.52", Low="84.52", Close="86.02", Volume="302000" },
            };





            ChartEngine engine = new ChartEngine();
            engine.Init(ProcessType.DEBUG);

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