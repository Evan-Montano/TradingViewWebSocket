# TradingView Real-Time Candlestick Listener

This project connects to [TradingView](https://www.tradingview.com)'s real-time chart WebSocket endpoint, subscribes to market data, and logs candlestick updates (OHLCV) for downstream processing. It's designed to be extensible, forming the foundation for future prediction and analysis layers—such as machine learning models for chart-based forecasting.

## Features

* Connects to TradingView's WebSocket feed
* Authenticates and subscribes to a specified chart symbol
* Parses and logs candlestick data in real-time (timestamp, OHLC, volume)
* Designed with modularity in mind to support future automation or AI features
* Works with any NASDAQ symbol

## Requirements

* .NET 6 SDK or newer
* Internet connection

## Usage

1. **Clone the repo**

   ```bash
   git clone https://github.com/Evan-Montano/TradingViewWebSocket.git
   cd TradingViewWebSocket
   ```

2. **Configure symbol & channel settings**
   Edit `Application.cs` to set:

   ```csharp
   string symbol = "NVDA"; // Example TradingView NASDAQ symbol
   string resolution = "1";           // 1-minute candles
   ```

3. **Build and run**

   ```bash
   dotnet run
   ```

4. **Observe logs**
   Candlestick data will be printed or persisted based on your current `CandlestickUpdateProcessor` implementation.

## Disclaimer

This project is for educational and research purposes. TradingView's WebSocket feed is undocumented and usage may be subject to change or limitations. Use responsibly.

## License

MIT License
