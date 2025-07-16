
using System.Net;
using System.Net.WebSockets;
using System.Text;
using System.Text.Json.Nodes;
using System.Threading.Tasks;
using TradingViewWebSocket;

class Program
{
    public static async Task Main(string[] args)
    {
        string CHART_SESSION_ID = string.Empty;
        string QUOTE_SESSION_ID = string.Empty;
        string CHART_SYMBOL = "NVDA"; // NVIDIA Corporation
        string BASE_URL = "wss://data.tradingview.com/socket.io/websocket";
        ClientWebSocket webSocket;
        Uri uri;
        
        try
        {
            CHART_SESSION_ID = GetSessionId("cs");
            QUOTE_SESSION_ID = GetSessionId("qs");
            webSocket = new ClientWebSocket();
            uri = new Uri(BuildWebSocketUri(BASE_URL));

            Console.WriteLine("TradingView WebSocket Client");
            Console.WriteLine($"Connecting to: {uri.ToString()}");


            // Connect to the WebSocket server
            await ConnectWebSocketAsync(webSocket, uri);

            SendMessages(webSocket, CHART_SESSION_ID, QUOTE_SESSION_ID, CHART_SYMBOL);

            BeginDataFeed(webSocket, CHART_SESSION_ID, QUOTE_SESSION_ID, CHART_SYMBOL);

            await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing connection", CancellationToken.None);
            Console.WriteLine("WebSocket connection closed.");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"An error occurred: {ex.Message}");
        }
    }

    private static async Task BeginDataFeed(ClientWebSocket webSocket, string CHART_SESSION_ID, string QUOTE_SESSION_ID, string CHART_SYMBOL)
    {
        // This method is a placeholder for any data feed initialization logic.
        Console.WriteLine("\nData feed initialization started...\n");
        StreamWriter logFile;
        DataHelper dataHelper;
        try
        {
            logFile = new StreamWriter("TradingViewWebSocket.log", append: true);
            dataHelper = new DataHelper();
            while (true)
            {
                // Read messages from the WebSocket
                ArraySegment<byte> buffer = new ArraySegment<byte>(new byte[1024]);
                WebSocketReceiveResult result = webSocket.ReceiveAsync(buffer, CancellationToken.None).GetAwaiter().GetResult();
                if (result.MessageType == WebSocketMessageType.Close)
                {
                    Console.WriteLine("WebSocket connection closed by server.");
                    break;
                }
                else if (result.MessageType == WebSocketMessageType.Text)
                {
                    string message = Encoding.UTF8.GetString(buffer.Array, 0, result.Count);
                    Console.WriteLine($"Received message: {message}");

                    if (message.Contains("series_loading"))
                    {
                        // We wait for the series_loading message to be received
                        // Now we will call the following:
                        // request_more_tickmarks
                        // create_study (x5) each for different study types
                        RequestMoreTickmarks(webSocket, CHART_SESSION_ID).Wait();
                        CreateStudy(webSocket, CHART_SESSION_ID).Wait();
                    }

                    // When receiving: ~m~4~m~~h~1
                    // This is a heartbeat message, which we must send back as is
                    if (message.Contains("~h~"))
                    {
                        try
                        {
                            await webSocket.SendAsync(
                                new ArraySegment<byte>(Encoding.UTF8.GetBytes(message)),
                                WebSocketMessageType.Text,
                                true,
                                CancellationToken.None);
                            Console.WriteLine("Sent message: " + message);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing heartbeat message: {ex.Message}");
                        }
                    }

                    // Check for the data update message
                    else if (message.Contains("\"m\": \"du\""))
                    {
                        try
                        {
                            dataHelper.ProcessDataUpdate(message, logFile);
                        }
                        catch (Exception ex)
                        {
                            Console.WriteLine($"Error processing data update message: {ex.Message}");
                        }
                    }
                        Console.WriteLine();
                }
                else if (result.MessageType == WebSocketMessageType.Binary)
                {
                    Console.WriteLine("Received binary data.");
                }
                else
                {
                    Console.WriteLine("Received unknown message type.");
                }

            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Error during data feed initialization: {ex.Message}");
        }
    }

    private static void SendMessages(ClientWebSocket webSocket, string CHART_SESSION_ID, string QUOTE_SESSION_ID, string CHART_SYMBOL)
    {
        // set_auth_token
        // set_locale
        // quote_create_session
        // quote_add_symbols
        // quote_set_fields
        // chart_create_session
        // resolve_symbol
        // create_series
        SetAuthToken(webSocket).Wait();
        SetLocale(webSocket).Wait();
        QuoteCreateSession(webSocket, QUOTE_SESSION_ID).Wait();
        QuoteSetFields(webSocket, QUOTE_SESSION_ID).Wait();
        QuoteAddSymbols(webSocket, QUOTE_SESSION_ID, CHART_SYMBOL).Wait();
        QuoteFastSymbols(webSocket, QUOTE_SESSION_ID, CHART_SYMBOL).Wait();
        ChartCreateSession(webSocket, CHART_SESSION_ID).Wait();
        ResolveSymbol(webSocket, CHART_SESSION_ID, CHART_SYMBOL).Wait();
        CreateSeries(webSocket, CHART_SESSION_ID).Wait();

        // Now we call quote remove symbols, quote add symbols twice, and add quote fast symbols one more time
        QuoteRemoveSymbols(webSocket, QUOTE_SESSION_ID, CHART_SYMBOL).Wait();
        // Doing both in one method
        QuoteAddSymbolsForPricingData(webSocket, QUOTE_SESSION_ID, CHART_SYMBOL).Wait();
        QuoteFastSymbolsForPricingData(webSocket, QUOTE_SESSION_ID, CHART_SYMBOL).Wait();
    }

    private static async Task CreateStudy(ClientWebSocket webSocket, string CHART_SESSION_ID)
    {
        // We will create 5 different studies each given their own call:
        // ~m~130~m~{"m":"create_study","p":["cs_a2UkqE70LfhA","st1","st1","sds_1","Volume@tv-basicstudies-251",{"length":20,"col_prev_close":false}]}
        // ~m~99~m~{"m":"create_study","p":["cs_a2UkqE70LfhA","st2","st1","sds_1","Dividends@tv-basicstudies-251",{}]}
        // ~m~96~m~{"m":"create_study","p":["cs_a2UkqE70LfhA","st3","st1","sds_1","Splits@tv-basicstudies-251",{}]}
        // ~m~98~m~{"m":"create_study","p":["cs_a2UkqE70LfhA","st4","st1","sds_1","Earnings@tv-basicstudies-251",{}]}
        // ~m~132~m~{"m":"create_study","p":["cs_a2UkqE70LfhA","st5","st1","sds_1","BarSetContinuousRollDates@tv-corestudies-30",{"currenttime":"now"}]}
        Console.Write("Creating studies... ");
        JsonObject payload = new JsonObject();
        payload["m"] = "create_study";
        payload["p"] = new JsonArray(CHART_SESSION_ID, "st1", "st1", "sds_1", "Volume@tv-basicstudies-251", new JsonObject { ["length"] = 20, ["col_prev_close"] = false });
        string payloadString = payload.ToJsonString();
        string frontLoad = $"~m~{payloadString.Length}~m~";
        frontLoad += payloadString;
        try
        {
            await webSocket.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(frontLoad)),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
            Console.WriteLine("Success");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed: {ex.Message}");
            return;
        }
        // Now we send the second payload for the second study
        payload = new JsonObject();
        payload["m"] = "create_study";
        payload["p"] = new JsonArray(CHART_SESSION_ID, "st2", "st1", "sds_1", "Dividends@tv-basicstudies-251", new JsonObject { });
        payloadString = payload.ToJsonString();
        frontLoad = $"~m~{payloadString.Length}~m~";
        frontLoad += payloadString;
        try
        {
            await webSocket.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(frontLoad)),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
            Console.WriteLine("Success");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed: {ex.Message}");
            return;
        }
        // Now we send the third payload for the third study
        payload = new JsonObject();
        payload["m"] = "create_study";
        payload["p"] = new JsonArray(CHART_SESSION_ID, "st3", "st1", "sds_1", "Splits@tv-basicstudies-251", new JsonObject { });
        payloadString = payload.ToJsonString();
        frontLoad = $"~m~{payloadString.Length}~m~";
        frontLoad += payloadString;
        try
        {
            await webSocket.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(frontLoad)),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
            Console.WriteLine("Success");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed: {ex.Message}");
            return;
        }
        // Now we send the fourth payload for the fourth study
        payload = new JsonObject();
        payload["m"] = "create_study";
        payload["p"] = new JsonArray(CHART_SESSION_ID, "st4", "st1", "sds_1", "Earnings@tv-basicstudies-251", new JsonObject { });
        payloadString = payload.ToJsonString();
        frontLoad = $"~m~{payloadString.Length}~m~";
        frontLoad += payloadString;
        try
        {
            await webSocket.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(frontLoad)),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
            Console.WriteLine("Success");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed: {ex.Message}");
            return;
        }
        // Now we send the fifth payload for the fifth study
        payload = new JsonObject();
        payload["m"] = "create_study";
        payload["p"] = new JsonArray(CHART_SESSION_ID, "st5", "st1", "sds_1", "BarSetContinuousRollDates@tv-corestudies-30", new JsonObject { ["currenttime"] = "now" });
        payloadString = payload.ToJsonString();
        frontLoad = $"~m~{payloadString.Length}~m~";
        frontLoad += payloadString;
        try
        {
            await webSocket.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(frontLoad)),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
            Console.WriteLine("Success");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed: {ex.Message}");
            return;
        }
    }

    private static async Task RequestMoreTickmarks(ClientWebSocket webSocket, string CHART_SESSION_ID)
    {
        // ~m~65~m~{"m":"request_more_tickmarks","p":["cs_a2UkqE70LfhA","sds_1",10]}
        Console.Write("Requesting more tickmarks... ");
        JsonObject payload = new JsonObject();
        payload["m"] = "request_more_tickmarks";
        payload["p"] = new JsonArray(CHART_SESSION_ID, "sds_1", 10);
        string payloadString = payload.ToJsonString();
        string frontLoad = $"~m~{payloadString.Length}~m~";
        frontLoad += payloadString;
        try
        {
            await webSocket.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(frontLoad)),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
            Console.WriteLine("Success");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed: {ex.Message}");
            return;
        }
    }

    private static async Task QuoteFastSymbolsForPricingData(ClientWebSocket webSocket, string QUOTE_SESSION_ID, string CHART_SYMBOL)
    {
        // ~m~144~m~{"m":"quote_fast_symbols","p":["qs_rpz9mlKi4q6S","={\"adjustment\":\"splits\",\"currency-id\":\"USD\",\"symbol\":\"BATS:NVDA\"}","NASDAQ:NVDA"]}
        Console.Write("Requesting fast symbols for pricing data... ");
        JsonObject payload = new JsonObject();
        payload["m"] = "quote_fast_symbols";
        payload["p"] = new JsonArray(QUOTE_SESSION_ID, $"={{\"adjustment\":\"splits\",\"currency-id\":\"USD\",\"symbol\":\"BATS:{CHART_SYMBOL}\"}}", $"NASDAQ:{CHART_SYMBOL}");
        string payloadString = payload.ToJsonString();
        string frontLoad = $"~m~{payloadString.Length}~m~";
        frontLoad += payloadString;
        try
        {
            await webSocket.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(frontLoad)),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
            Console.WriteLine("Success");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed: {ex.Message}");
            return;
        }
    }

    private static async Task QuoteAddSymbolsForPricingData(ClientWebSocket webSocket, string QUOTE_SESSION_ID, string CHART_SYMBOL)
    {
        // ~m~129~m~{"m":"quote_add_symbols","p":["qs_rpz9mlKi4q6S","={\"adjustment\":\"splits\",\"currency-id\":\"USD\",\"symbol\":\"BATS:NVDA\"}"]}
        // then:
        // ~m~63~m~{"m":"quote_add_symbols","p":["qs_rpz9mlKi4q6S","NASDAQ:NVDA"]}

        Console.Write("Adding symbols for pricing data... ");
        JsonObject payload = new JsonObject();
        payload["m"] = "quote_add_symbols";
        payload["p"] = new JsonArray(QUOTE_SESSION_ID, $"={{\"adjustment\":\"splits\",\"currency-id\":\"USD\",\"symbol\":\"BATS:{CHART_SYMBOL}\"}}");
        string payloadString = payload.ToJsonString();
        string frontLoad = $"~m~{payloadString.Length}~m~";
        frontLoad += payloadString;
        try
        {
            await webSocket.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(frontLoad)),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
            Console.WriteLine("Success");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed: {ex.Message}");
            return;
        }
        // Now we send the second payload for pricing data
        Console.Write("Adding symbols for pricing data (second payload)... ");
        payload = new JsonObject();
        payload["m"] = "quote_add_symbols";
        payload["p"] = new JsonArray(QUOTE_SESSION_ID, $"NASDAQ:{CHART_SYMBOL}");
        payloadString = payload.ToJsonString();
        frontLoad = $"~m~{payloadString.Length}~m~";
        frontLoad += payloadString;
        try
        {
            await webSocket.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(frontLoad)),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
            Console.WriteLine("Success");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed: {ex.Message}");
            return;
        }
    }

    private static async Task QuoteRemoveSymbols(ClientWebSocket webSocket, string QUOTE_SESSION_ID, string CHART_SYMBOL)
    {
        // ~m~110~m~{"m":"quote_remove_symbols","p":["qs_rpz9mlKi4q6S","={\"adjustment\":\"splits\",\"symbol\":\"NASDAQ:NVDA\"}"]}
        Console.Write("Removing symbols from quote session... ");
        JsonObject payload = new JsonObject();
        payload["m"] = "quote_remove_symbols";
        payload["p"] = new JsonArray(QUOTE_SESSION_ID, $"={{\"adjustment\":\"splits\",\"symbol\":\"NASDAQ:{CHART_SYMBOL}\"}}");
        string payloadString = payload.ToJsonString();
        string frontLoad = $"~m~{payloadString.Length}~m~";
        frontLoad += payloadString;
        try
        {
            await webSocket.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(frontLoad)),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
            Console.WriteLine("Success");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed: {ex.Message}");
            return;
        }
    }

    private static async Task QuoteFastSymbols(ClientWebSocket webSocket, string QUOTE_SESSION_ID, string CHART_SYMBOL)
    {
        // ~m~108~m~{"m":"quote_fast_symbols","p":["qs_Xo2qPHr1wBBC","={\"adjustment\":\"splits\",\"symbol\":\"NASDAQ:NVDA\"}"]}
        Console.Write("Requesting fast symbols... ");
        JsonObject payload = new JsonObject();
        payload["m"] = "quote_fast_symbols";
        payload["p"] = new JsonArray(QUOTE_SESSION_ID, $"={{\"adjustment\":\"splits\",\"symbol\":\"NASDAQ:{CHART_SYMBOL}\"}}");
        string payloadString = payload.ToJsonString();
        string frontLoad = $"~m~{payloadString.Length}~m~";
        frontLoad += payloadString;
        try
        {
            await webSocket.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(frontLoad)),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
            Console.WriteLine("Success");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed: {ex.Message}");
            return;
        }
    }

    private static async Task QuoteAddSymbols(ClientWebSocket webSocket, string QUOTE_SESSION_ID, string CHART_SYMBOL)
    {
        // ~m~107~m~{"m":"quote_add_symbols","p":["qs_Xo2qPHr1wBBC","={\"adjustment\":\"splits\",\"symbol\":\"NASDAQ:NVDA\"}"]}
        Console.Write("Adding symbols to quote session... ");
        JsonObject payload = new JsonObject();
        payload["m"] = "quote_add_symbols";
        payload["p"] = new JsonArray(QUOTE_SESSION_ID, $"={{\"adjustment\":\"splits\",\"symbol\":\"NASDAQ:{CHART_SYMBOL}\"}}");
        string payloadString = payload.ToJsonString();
        string frontLoad = $"~m~{payloadString.Length}~m~";
        frontLoad += payloadString;
        try
        {
            await webSocket.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(frontLoad)),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
            Console.WriteLine("Success");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed: {ex.Message}");
            return;
        }
    }

    private static async Task QuoteSetFields(ClientWebSocket webSocket, string QUOTE_SESSION_ID)
    {
        // ~m~473~m~{"m":"quote_set_fields","p":["qs_GHP2eS4wsstl","base-currency-logoid","ch","chp","currency-logoid","currency_code","currency_id","base_currency_id","current_session","description","exchange","format","fractional","is_tradable","language","local_description","listed_exchange","logoid","lp","lp_time","minmov","minmove2","original_name","pricescale","pro_name","short_name","type","typespecs","update_mode","volume","variable_tick_size","value_unit_id","unit_id","measure"]}
        Console.Write("Setting quote fields... ");
        JsonObject payload = new JsonObject();
        payload["m"] = "quote_set_fields";
        payload["p"] = new JsonArray(
            QUOTE_SESSION_ID,
            "base-currency-logoid",
            "ch",
            "chp",
            "currency-logoid",
            "currency_code",
            "currency_id",
            "base_currency_id",
            "current_session",
            "description",
            "exchange",
            "format",
            "fractional",
            "is_tradable",
            "language",
            "local_description",
            "listed_exchange",
            "logoid",
            "lp",
            "lp_time",
            "minmov",
            "minmove2",
            "original_name",
            "pricescale",
            "pro_name",
            "short_name",
            "type",
            "typespecs",
            "update_mode",
            "volume",
            "variable_tick_size",
            "value_unit_id",
            "unit_id",
            "measure");
        string payloadString = payload.ToJsonString();
        string frontLoad = $"~m~{payloadString.Length}~m~";
        frontLoad += payloadString;
        try
        {
            await webSocket.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(frontLoad)),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
            Console.WriteLine("Success");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed: {ex.Message}");
            return;
        }
    }

    private static async Task CreateSeries(ClientWebSocket webSocket, string CHART_SESSION_ID)
    {
        // ~m~81~m~{"m":"create_series","p":["cs_5E9H2p8mfqAs","sds_1","s1","sds_sym_1","D",300,""]}

        // ~m~81~m~{"m":"create_series","p":["cs_a6TBnrCB5vSw","sds_1","s1","sds_sym_1","D",300,""]}
        // 2 Examples above
        Console.Write("Creating series... ");
        JsonObject payload = new JsonObject();
        payload["m"] = "create_series";
        payload["p"] = new JsonArray(CHART_SESSION_ID, "sds_1", "s1", "sds_sym_1", "D", 300, "");
        string payloadString = payload.ToJsonString();
        string frontLoad = $"~m~{payloadString.Length}~m~";
        frontLoad += payloadString;
        try
        {
            await webSocket.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(frontLoad)),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
            Console.WriteLine("Success");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed: {ex.Message}");
            return;
        }
    }

    private static async Task ResolveSymbol(ClientWebSocket webSocket, string CHART_SESSION_ID, string CHART_SYMBOL)
    {
        // ~m~116~m~{"m":"resolve_symbol","p":["cs_dk9yKp8jrTJX","sds_sym_1","={\"adjustment\":\"splits\",\"symbol\":\"NASDAQ:NVDA\"}"]}

        // ~m~116~m~{"m":"resolve_symbol","p":["cs_5E9H2p8mfqAs","sds_sym_1","={\"adjustment\":\"splits\",\"symbol\":\"NASDAQ:NVDA\"}"]}
        // 2 Examples above

        Console.Write("Resolving symbol... ");
        JsonObject payload = new JsonObject();
        payload["m"] = "resolve_symbol";
        payload["p"] = new JsonArray(CHART_SESSION_ID, "sds_sym_1", $"={{\"adjustment\":\"splits\",\"symbol\":\"NASDAQ:{CHART_SYMBOL}\"}}");
        string payloadString = payload.ToJsonString();
        string frontLoad = $"~m~{payloadString.Length}~m~";
        frontLoad += payloadString;

        try
        {
            await webSocket.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(frontLoad)),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
            Console.WriteLine("Success");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed: {ex.Message}");
            return;
        }
    }

    private static async Task ChartCreateSession(ClientWebSocket webSocket, string CHART_SESSION_ID)
    {
        // ~m~55~m~{"m":"chart_create_session","p":["cs_1f0C5y0nZfLX",""]} -- the second parameter appears to be empty consistently
        Console.Write("Creating chart session... ");
        JsonObject payload = new JsonObject();
        payload["m"] = "chart_create_session";
        payload["p"] = new JsonArray(CHART_SESSION_ID, "");
        string payloadString = payload.ToJsonString();
        string frontLoad = $"~m~{payloadString.Length}~m~";
        frontLoad += payloadString;
        try
        {
            await webSocket.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(frontLoad)),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
            Console.WriteLine("Success");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed: {ex.Message}");
            return;
        }
    }

    private static async Task QuoteCreateSession(ClientWebSocket webSocket, string QUOTE_CREATE_SESSION)
    {
        // ~m~52~m~{"m":"quote_create_session","p":["qs_vwy5BKVIhVn6"]}
        Console.Write("Creating quote session... ");
        JsonObject payload = new JsonObject();
        payload["m"] = "quote_create_session";
        payload["p"] = new JsonArray(QUOTE_CREATE_SESSION);
        string payloadString = payload.ToJsonString();
        string frontLoad = $"~m~{payloadString.Length}~m~";
        frontLoad += payloadString;
        try
        {
            await webSocket.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(frontLoad)),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
            Console.WriteLine("Success");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed: {ex.Message}");
            return;
        }
    }

    private static async Task SetLocale(ClientWebSocket webSocket)
    {
        // ~m~34~m~{"m":"set_locale","p":["en","US"]}
        Console.Write("Setting locale... ");
        JsonObject payload = new JsonObject();
        payload["m"] = "set_locale";
        payload["p"] = new JsonArray("en", "US");
        string payloadString = payload.ToJsonString();
        string frontLoad = $"~m~{payloadString.Length}~m~";
        frontLoad += payloadString;

        try
        {
            await webSocket.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(frontLoad)),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
            Console.WriteLine("Success");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed: {ex.Message}");
            return;
        }
    }

    private static async Task SetAuthToken(ClientWebSocket webSocket)
    {
        // ~m~54~m~{"m":"set_auth_token","p":["unauthorized_user_token"]}
        Console.Write("Setting auth token... ");
        JsonObject payload = new JsonObject();
        payload["m"] = "set_auth_token";
        payload["p"] = new JsonArray("unauthorized_user_token");
        string payloadString = payload.ToJsonString();
        string frontLoad = $"~m~{payloadString.Length}~m~";
        frontLoad += payloadString;

        try
        {
            await webSocket.SendAsync(
                new ArraySegment<byte>(Encoding.UTF8.GetBytes(frontLoad)),
                WebSocketMessageType.Text,
                true,
                CancellationToken.None);
            Console.WriteLine("Success");
        }
        catch (Exception ex)
        {
            Console.WriteLine($"Failed: {ex.Message}");
            return;
        }
    }

    private static string BuildWebSocketUri(string BASE_URL)
    {
        var chartId = "chart/c1MExidS/"; // NVDA - NVIDIA Corporation
        var from = WebUtility.UrlEncode("chart/");
        var date = WebUtility.UrlEncode("2025-07-14T18:28:04");
        var type = WebUtility.UrlEncode("chart");

        var fullUrl = $"{BASE_URL}?from={from}&date={date}&type={type}";
        return fullUrl;
    }

    private static string GetSessionId(string prefix)
    {
        var rand = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
            .Replace("=", string.Empty)
            .Replace("+", string.Empty)
            .Replace("/", string.Empty)
            .Substring(0, 12);
        return $"{prefix}_{rand}";
    }

    private async static Task ConnectWebSocketAsync(ClientWebSocket webSocket, Uri uri)
    {
        try
        {
            webSocket.Options.SetRequestHeader("Origin", "https://data.tradingview.com");

            await webSocket.ConnectAsync(uri, CancellationToken.None);
            Console.WriteLine("Connected to WebSocket server.");
        }
        catch (WebSocketException ex)
        {
            Console.WriteLine($"WebSocket error: {ex.Message}");
            return;
        }
    }
}