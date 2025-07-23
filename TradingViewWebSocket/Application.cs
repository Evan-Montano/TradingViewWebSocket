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
            var client = new WebSocketClient(SYMBOL);
            await client.RunAsync();
            
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

}