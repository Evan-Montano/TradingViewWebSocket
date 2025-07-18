using TradingViewWebSocket;

class Application
{
    /// <summary>
    /// Main entry point of the application.
    /// We define the NASDAQ symbol we will be using.
    /// </summary>
    /// <param name="args"></param>
    /// <returns></returns>
    public static async Task Main(string[] args)
    {
        const string SYMBOL = "NVDA";
        
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