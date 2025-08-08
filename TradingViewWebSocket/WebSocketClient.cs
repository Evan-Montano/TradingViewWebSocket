using System.Collections.Concurrent;
using System.Net;
using System.Net.WebSockets;
using System.Runtime.CompilerServices;
using System.Text;
using System.Text.Json.Nodes;

namespace TradingViewWebSocket
{
    public class WebSocketClient
    {
        private const string DAILY = "D";
        private const string ONE_MINUTE = "1";
        private const string FIVE_MINUTE = "5";
        private const string BASE_URL = "wss://data.tradingview.com/socket.io/websocket";
        private string CHART_SESSION_ID = string.Empty;
        private string QUOTE_SESSION_ID = string.Empty;
        private string CHART_SYMBOL = string.Empty;
        
        ClientWebSocket webSocket;
        Uri uri;
        ProcessType processType;

        /// <summary>
        /// Initializer for WebSocketClient.
        /// Here we are setting key values and initializing objects to use
        /// </summary>
        /// <param name="symbol_">NASDAQ symbol</param>
        public WebSocketClient(string symbol_, ProcessType processType_)
        {
            this.CHART_SYMBOL = symbol_;

            CHART_SESSION_ID = GetSessionId("cs");
            QUOTE_SESSION_ID = GetSessionId("qs");
            webSocket = new ClientWebSocket();
            uri = new Uri(BuildWebSocketUri(BASE_URL));
        }

        /// <summary>
        /// Connects the websocket to the server and begins accepting data updates
        /// for the chart candlesticks (and supporting data)
        /// </summary>
        /// <returns></returns>
        public async Task RunAsync()
        {
            try
            {
                // Connect to the WebSocket server
                await ConnectWebSocketAsync(webSocket, uri);

                SendMessages(webSocket, CHART_SESSION_ID, QUOTE_SESSION_ID, CHART_SYMBOL);

                BeginDataFeed(webSocket, CHART_SESSION_ID, QUOTE_SESSION_ID, CHART_SYMBOL, this.processType);

                await webSocket.CloseAsync(WebSocketCloseStatus.NormalClosure, "Closing connection", CancellationToken.None);
                Console.WriteLine("WebSocket connection closed.");
            }
            catch (Exception ex)
            {
                Console.WriteLine(ex.ToString());
                return;
            }
        }

        /// <summary>
        /// Begins receiving and processing data from the WebSocket connection. 
        /// Reads incoming messages continuously, handles heartbeat, series loading, and data updates.
        /// </summary>
        /// <param name="webSocket">The WebSocket client to read from.</param>
        /// <param name="CHART_SESSION_ID">Session identifier for chart stream.</param>
        /// <param name="QUOTE_SESSION_ID">Session identifier for quote stream.</param>
        /// <param name="CHART_SYMBOL">Ticker symbol for which data is streamed.</param>

        private static async Task BeginDataFeed(ClientWebSocket webSocket, string CHART_SESSION_ID, string QUOTE_SESSION_ID, string CHART_SYMBOL, ProcessType processType)
        {
            // This method is a placeholder for any data feed initialization logic.
            Console.WriteLine("\nData feed initialization started...\n");
            DataHelper dataHelper;

            try
            {
                dataHelper = new DataHelper(processType, CHART_SYMBOL);

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
                        if (message.Contains("~h~") && !message.Contains("sds_1"))
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
                        else if (message.Contains("\"m\":\"du\"") && message.Contains("sds_1"))
                        {
                            try
                            {
                                Task.Run(() => dataHelper.ProcessDataUpdate(message, CHART_SYMBOL));
                            }
                            catch (Exception ex)
                            {
                                Console.WriteLine($"Error processing data update message: {ex.Message}");
                                break;
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
                dataHelper.Dispose();
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Error during data feed initialization: {ex.Message}");
            }
        }

        #region WebSocketMessages
        /// <summary>
        /// Sends the initial sequence of WebSocket messages to authenticate, configure locale, 
        /// create sessions, resolve symbol, and set up chart series and quote subscriptions.
        /// </summary>
        /// <param name="webSocket">The WebSocket client used for sending messages.</param>
        /// <param name="CHART_SESSION_ID">Chart session identifier.</param>
        /// <param name="QUOTE_SESSION_ID">Quote session identifier.</param>
        /// <param name="CHART_SYMBOL">Ticker symbol used in messages.</param>
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

        /// <summary>
        /// Sends commands to create multiple studies (Volume, Dividends, Splits, Earnings, BarSetContinuousRollDates)
        /// on the TradingView chart session.
        /// </summary>
        /// <param name="webSocket">The WebSocket client used to issue create_study commands.</param>
        /// <param name="CHART_SESSION_ID">Chart session identifier value.</param>
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

        /// <summary>
        /// Requests additional tick marks (time scale markers) for the given chart session.
        /// </summary>
        /// <param name="webSocket">The WebSocket client to send the request.</param>
        /// <param name="CHART_SESSION_ID">Chart session identifier where tickmarks are requested.</param>
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

        /// <summary>
        /// Sends a quote_fast_symbols command to request pricing data for the specified symbol with split adjustment.
        /// </summary>
        /// <param name="webSocket">The WebSocket client used to send the command.</param>
        /// <param name="QUOTE_SESSION_ID">Quote session identifier for the data request.</param>
        /// <param name="CHART_SYMBOL">Ticker symbol for pricing data.</param>
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

        /// <summary>
        /// Adds symbols for pricing data with both split-adjusted and regular formats for the specified symbol.
        /// </summary>
        /// <param name="webSocket">The WebSocket client used to issue add_symbols commands.</param>
        /// <param name="QUOTE_SESSION_ID">Quote session identifier.</param>
        /// <param name="CHART_SYMBOL">Ticker symbol to add.</param>
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

        /// <summary>
        /// Sends a command to remove the specified symbol from the quote session.
        /// </summary>
        /// <param name="webSocket">The WebSocket client used to send the remove command.</param>
        /// <param name="QUOTE_SESSION_ID">Quote session identifier.</param>
        /// <param name="CHART_SYMBOL">Ticker symbol to remove.</param>
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

        /// <summary>
        /// Sends a quote_fast_symbols command to request fast quote updates with split adjustment.
        /// </summary>
        /// <param name="webSocket">The WebSocket client used to send the command.</param>
        /// <param name="QUOTE_SESSION_ID">Quote session identifier.</param>
        /// <param name="CHART_SYMBOL">Ticker symbol to query.</param>
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

        /// <summary>
        /// Adds the specified symbol to the quote session subscription with split-adjustment metadata.
        /// </summary>
        /// <param name="webSocket">The WebSocket client used to send the command.</param>
        /// <param name="QUOTE_SESSION_ID">Quote session identifier.</param>
        /// <param name="CHART_SYMBOL">Ticker symbol to subscribe to.</param>
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

        /// <summary>
        /// Sends a quote_set_fields command to configure which quote data fields should be returned.
        /// </summary>
        /// <param name="webSocket">The WebSocket client used to send the command.</param>
        /// <param name="QUOTE_SESSION_ID">Quote session identifier used in the command.</param>
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

        /// <summary>
        /// Creates a series subscription on the chart session for fetching historical candle data.
        /// </summary>
        /// <param name="webSocket">The WebSocket client used to send the command.</param>
        /// <param name="CHART_SESSION_ID">Chart session identifier for the series.</param>
        private static async Task CreateSeries(ClientWebSocket webSocket, string CHART_SESSION_ID)
        {
            // ~m~81~m~{"m":"create_series","p":["cs_5E9H2p8mfqAs","sds_1","s1","sds_sym_1","D",300,""]}

            // ~m~81~m~{"m":"create_series","p":["cs_a6TBnrCB5vSw","sds_1","s1","sds_sym_1","D",300,""]}
            // 2 Examples above
            Console.Write("Creating series... ");
            JsonObject payload = new JsonObject();
            payload["m"] = "create_series";
            payload["p"] = new JsonArray(CHART_SESSION_ID, "sds_1", "s1", "sds_sym_1", ONE_MINUTE /* 1-minute chart */, 10 /* Number of candles to request initially */, "");
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

        /// <summary>
        /// Resolves the specified ticker symbol in the chart session, setting it up for data retrieval.
        /// </summary>
        /// <param name="webSocket">The WebSocket client used to send resolve_symbol command.</param>
        /// <param name="CHART_SESSION_ID">Chart session identifier.</param>
        /// <param name="CHART_SYMBOL">Ticker symbol to resolve.</param>
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

        /// <summary>
        /// Creates a new chart session with the specified session identifier.
        /// </summary>
        /// <param name="webSocket">The WebSocket client used to send chart_create_session command.</param>
        /// <param name="CHART_SESSION_ID">Chart session identifier to create.</param>
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

        /// <summary>
        /// Creates a new quote session using the provided identifier.
        /// </summary>
        /// <param name="webSocket">The WebSocket client used to send quote_create_session command.</param>
        /// <param name="QUOTE_CREATE_SESSION">Quote session identifier to create.</param>
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

        /// <summary>
        /// Sends a set_locale command to localize responses (e.g., language, currency).
        /// </summary>
        /// <param name="webSocket">The WebSocket client used to send the command.</param>
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

        /// <summary>
        /// Sends a set_auth_token command to authenticate the user/session with the TradingView service.
        /// </summary>
        /// <param name="webSocket">The WebSocket client used to send the authorization token.</param>
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
        #endregion WebSocketMessages

        /// <summary>
        /// Opens a WebSocket connection to the specified URI, setting necessary headers.
        /// </summary>
        /// <param name="webSocket">The WebSocket client instance to connect.</param>
        /// <param name="uri">Destination URI for the WebSocket connection.</param>
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

        /// <summary>
        /// Constructs the full TradingView WebSocket URI including parameters for chart initialization.
        /// </summary>
        /// <param name="BASE_URL">The base TradingView WebSocket endpoint.</param>
        /// <returns>Complete URI string with query parameters for chart connection.</returns>
        private static string BuildWebSocketUri(string BASE_URL)
        {
            var chartId = "chart/c1MExidS/"; // NVDA - NVIDIA Corporation
            var from = WebUtility.UrlEncode("chart/");
            var date = WebUtility.UrlEncode("2025-07-14T18:28:04");
            var type = WebUtility.UrlEncode("chart");

            var fullUrl = $"{BASE_URL}?from={from}&date={date}&type={type}";
            return fullUrl;
        }

        /// <summary>
        /// Generates a unique session identifier based on the given prefix and a randomized GUID-derived string.
        /// </summary>
        /// <param name="prefix">Prefix to categorize the session (e.g., "cs" or "qs").</param>
        /// <returns>A unique session ID string.</returns>
        private static string GetSessionId(string prefix)
        {
            var rand = Convert.ToBase64String(Guid.NewGuid().ToByteArray())
                .Replace("=", string.Empty)
                .Replace("+", string.Empty)
                .Replace("/", string.Empty)
                .Substring(0, 12);
            return $"{prefix}_{rand}";
        }
    }
}
