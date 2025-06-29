# Geidea POS Terminal Integration (USB)

This is a C# console application to integrate with the **Geidea Palm POS Terminal (Model P01)** over USB, Serial, or Network.

## 🔧 Features
- USB / Serial / Network support
- Auto COM port detection
- Sale, Refund, Void operations
- Terminal status and connection testing

## 📦 Requirements
- .NET 6 SDK
- Geidea POS terminal with USB integration enabled
- macOS (or Windows for full serial support)

## 🧰 Setup

1. **Clone or Create Project**
   ```bash
   dotnet new console -n GeideaPOSIntegration
````

2. **Install Required Packages**

   ```bash
   dotnet add package System.IO.Ports
   dotnet add package System.Management
   dotnet add package Microsoft.Extensions.Configuration
   dotnet add package Microsoft.Extensions.Configuration.Json
   dotnet add package Microsoft.Extensions.Logging.Console
   dotnet add package Microsoft.Extensions.Hosting
   ```

3. **Configure `appsettings.json`**

   ```json
   {
     "GeideaSettings": {
       "MerchantId": "YOUR_MERCHANT_ID",
       "TerminalId": "YOUR_TERMINAL_ID",
       "ConnectionType": "USB",
       "SerialPort": {
         "BaudRate": 9600,
         "DataBits": 8,
         "Parity": "None",
         "StopBits": "One",
         "TimeoutMs": 30000
       },
       "Network": {
         "BaseUrl": "http://192.168.1.100:8080",
         "TimeoutMs": 30000
       }
     },
     "Logging": {
       "LogLevel": {
         "Default": "Information"
       }
     }
   }
   ```

4. **Run the App**

   ```bash
   dotnet run
   ```

## 📍 Where to Get Merchant & Terminal IDs

* [Geidea Merchant Portal](https://www.merchant.geidea.net/sa/sign-in?language=en)
* Terminal settings under *Device Info*
* Contact Geidea support if unavailable

