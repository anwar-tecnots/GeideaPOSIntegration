using System.IO.Ports;
using System.Management;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using GeideaPOSIntegration.Configuration;
using GeideaPOSIntegration.Models;

namespace GeideaPOSIntegration.Services
{
    public class GeideaTerminalService : IGeideaTerminalService, IDisposable
    {
        private readonly GeideaSettings _settings;
        private readonly ILogger<GeideaTerminalService> _logger;
        private readonly HttpClient _httpClient;
        private SerialPort? _serialPort;
        private bool _isConnected = false;
        private readonly object _lockObject = new();

        public GeideaTerminalService(IOptions<GeideaSettings> settings, ILogger<GeideaTerminalService> logger)
        {
            _settings = settings.Value;
            _logger = logger;
            _httpClient = new HttpClient();
            _httpClient.Timeout = TimeSpan.FromMilliseconds(_settings.Network.TimeoutMs);
        }

        public async Task<bool> InitializeAsync()
        {
            try
            {
                _logger.LogInformation("Initializing Geidea terminal connection...");

                var connectionType = Enum.Parse<ConnectionType>(_settings.ConnectionType, true);

                switch (connectionType)
                {
                    case ConnectionType.USB:
                    case ConnectionType.Serial:
                        return await InitializeSerialConnectionAsync();
                    case ConnectionType.Network:
                        return await InitializeNetworkConnectionAsync();
                    default:
                        _logger.LogError("Unsupported connection type: {ConnectionType}", _settings.ConnectionType);
                        return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to initialize terminal connection");
                return false;
            }
        }

        private async Task<bool> InitializeSerialConnectionAsync()
        {
            try
            {
                string? portName = await DetectTerminalPortAsync();

                if (string.IsNullOrEmpty(portName))
                {
                    _logger.LogWarning("No terminal port detected, using first available port");
                    portName = SerialPort.GetPortNames().FirstOrDefault();
                }

                if (string.IsNullOrEmpty(portName))
                {
                    _logger.LogError("No COM ports available");
                    return false;
                }

                _logger.LogInformation("Connecting to terminal on port: {PortName}", portName);

                var parity = Enum.Parse<Parity>(_settings.SerialPort.Parity, true);
                var stopBits = Enum.Parse<StopBits>(_settings.SerialPort.StopBits, true);

                _serialPort = new SerialPort(portName, _settings.SerialPort.BaudRate, parity,
                    _settings.SerialPort.DataBits, stopBits)
                {
                    ReadTimeout = _settings.SerialPort.TimeoutMs,
                    WriteTimeout = _settings.SerialPort.TimeoutMs,
                    Handshake = Handshake.None,
                    RtsEnable = true,
                    DtrEnable = true
                };

                _serialPort.Open();
                _isConnected = true;

                // Send initialization command
                var initResult = await SendCommandAsync("INIT", new Dictionary<string, object>
                {
                    {"MerchantId", _settings.MerchantId},
                    {"TerminalId", _settings.TerminalId}
                });

                if (initResult.Success)
                {
                    _logger.LogInformation("Terminal initialized successfully");
                    return true;
                }
                else
                {
                    _logger.LogWarning("Terminal initialization failed: {Message}", initResult.ResponseMessage);
                    return false;
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Serial connection initialization failed");
                return false;
            }
        }

        private async Task<bool> InitializeNetworkConnectionAsync()
        {
            try
            {
                var initData = new
                {
                    merchantId = _settings.MerchantId,
                    terminalId = _settings.TerminalId,
                    timestamp = DateTime.UtcNow
                };

                var content = new StringContent(JsonSerializer.Serialize(initData),
                    Encoding.UTF8, "application/json");

                var response = await _httpClient.PostAsync($"{_settings.Network.BaseUrl}/api/init", content);

                _isConnected = response.IsSuccessStatusCode;

                if (_isConnected)
                {
                    _logger.LogInformation("Network connection initialized successfully");
                }
                else
                {
                    _logger.LogWarning("Network connection initialization failed: {StatusCode}", response.StatusCode);
                }

                return _isConnected;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Network connection initialization failed");
                return false;
            }
        }

        private async Task<string?> DetectTerminalPortAsync()
        {
            try
            {
                var ports = SerialPort.GetPortNames();
                _logger.LogInformation("Available COM ports: {Ports}", string.Join(", ", ports));

                foreach (string port in ports)
                {
                    try
                    {
                        using var testPort = new SerialPort(port, _settings.SerialPort.BaudRate)
                        {
                            ReadTimeout = 2000,
                            WriteTimeout = 2000
                        };

                        testPort.Open();
                        testPort.WriteLine("TEST");
                        await Task.Delay(500);

                        if (testPort.BytesToRead > 0)
                        {
                            string response = testPort.ReadExisting();
                            if (response.Contains("GEIDEA", StringComparison.OrdinalIgnoreCase) ||
                                response.Contains("ACK", StringComparison.OrdinalIgnoreCase))
                            {
                                _logger.LogInformation("Terminal detected on port: {Port}", port);
                                return port;
                            }
                        }
                    }
                    catch (Exception ex)
                    {
                        _logger.LogDebug("Port {Port} test failed: {Error}", port, ex.Message);
                    }
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Port detection failed");
            }

            return null;
        }

        public async Task<PaymentResponse> ProcessPaymentAsync(PaymentRequest request)
        {
            if (!_isConnected)
            {
                return new PaymentResponse
                {
                    Success = false,
                    ResponseCode = "999",
                    ResponseMessage = "Terminal not connected"
                };
            }

            try
            {
                _logger.LogInformation("Processing payment: {TransactionId}, Amount: {Amount} {Currency}",
                    request.TransactionId, request.Amount, request.Currency);

                var parameters = new Dictionary<string, object>
                {
                    {"Amount", Math.Round(request.Amount * 100).ToString("F0")},
                    {"Currency", request.Currency},
                    {"TransactionType", request.PaymentType.ToString()},
                    {"TransactionId", request.TransactionId},
                    {"Description", request.Description ?? ""},
                    {"Timestamp", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")}
                };

                // Add additional data
                foreach (var kvp in request.AdditionalData)
                {
                    parameters[kvp.Key] = kvp.Value;
                }

                var response = await SendCommandAsync("PAYMENT", parameters);
                var paymentResponse = MapToPaymentResponse(response, request);

                _logger.LogInformation("Payment processed: {Success}, Code: {Code}, Message: {Message}",
                    paymentResponse.Success, paymentResponse.ResponseCode, paymentResponse.ResponseMessage);

                return paymentResponse;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Payment processing error");
                return new PaymentResponse
                {
                    Success = false,
                    ResponseCode = "998",
                    ResponseMessage = $"Payment processing error: {ex.Message}",
                    TransactionId = request.TransactionId
                };
            }
        }

        public async Task<PaymentResponse> RefundTransactionAsync(string originalTransactionId, decimal amount)
        {
            var refundRequest = new PaymentRequest
            {
                Amount = amount,
                TransactionId = Guid.NewGuid().ToString(),
                PaymentType = PaymentType.Refund,
                Description = $"Refund for {originalTransactionId}"
            };

            refundRequest.AdditionalData["OriginalTransactionId"] = originalTransactionId;

            return await ProcessPaymentAsync(refundRequest);
        }

        public async Task<PaymentResponse> VoidTransactionAsync(string transactionId)
        {
            var voidRequest = new PaymentRequest
            {
                Amount = 0,
                TransactionId = Guid.NewGuid().ToString(),
                PaymentType = PaymentType.Void,
                Description = $"Void transaction {transactionId}"
            };

            voidRequest.AdditionalData["OriginalTransactionId"] = transactionId;

            return await ProcessPaymentAsync(voidRequest);
        }

        public async Task<TerminalStatus> GetTerminalStatusAsync()
        {
            try
            {
                if (!_isConnected)
                {
                    return new TerminalStatus
                    {
                        IsConnected = false,
                        IsReady = false,
                        Status = "Disconnected",
                        ErrorMessage = "Terminal not connected"
                    };
                }

                var response = await SendCommandAsync("STATUS", new Dictionary<string, object>());

                return new TerminalStatus
                {
                    IsConnected = true,
                    IsReady = response.Success,
                    Status = response.Success ? "Ready" : "Error",
                    LastCommunication = DateTime.Now,
                    ErrorMessage = response.Success ? null : response.ResponseMessage
                };
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to get terminal status");
                return new TerminalStatus
                {
                    IsConnected = false,
                    IsReady = false,
                    Status = "Error",
                    ErrorMessage = ex.Message
                };
            }
        }

        public async Task<bool> TestConnectionAsync()
        {
            try
            {
                if (!_isConnected) return false;

                var response = await SendCommandAsync("TEST", new Dictionary<string, object>
                {
                    {"Timestamp", DateTime.UtcNow.ToString("yyyy-MM-ddTHH:mm:ssZ")}
                });

                return response.Success;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Connection test failed");
                return false;
            }
        }

        private async Task<TerminalResponse> SendCommandAsync(string command, Dictionary<string, object> parameters)
        {
            lock (_lockObject)
            {
                if (!_isConnected)
                {
                    return new TerminalResponse
                    {
                        Success = false,
                        ResponseCode = "999",
                        ResponseMessage = "Terminal not connected"
                    };
                }
            }

            try
            {
                var connectionType = Enum.Parse<ConnectionType>(_settings.ConnectionType, true);

                switch (connectionType)
                {
                    case ConnectionType.USB:
                    case ConnectionType.Serial:
                        return await SendSerialCommandAsync(command, parameters);
                    case ConnectionType.Network:
                        return await SendNetworkCommandAsync(command, parameters);
                    default:
                        throw new NotSupportedException($"Connection type {connectionType} not supported");
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Command send failed: {Command}", command);
                return new TerminalResponse
                {
                    Success = false,
                    ResponseCode = "997",
                    ResponseMessage = ex.Message
                };
            }
        }

        private async Task<TerminalResponse> SendSerialCommandAsync(string command, Dictionary<string, object> parameters)
        {
            if (_serialPort == null || !_serialPort.IsOpen)
                throw new InvalidOperationException("Serial port not open");

            string message = BuildMessage(command, parameters);
            _logger.LogDebug("Sending command: {Message}", message);

            lock (_lockObject)
            {
                _serialPort.DiscardInBuffer();
                _serialPort.DiscardOutBuffer();
                _serialPort.WriteLine(message);
            }

            await Task.Delay(100); // Brief delay for command processing

            // Wait for response
            var startTime = DateTime.Now;
            var responseBuilder = new StringBuilder();

            while ((DateTime.Now - startTime).TotalMilliseconds < _settings.SerialPort.TimeoutMs)
            {
                lock (_lockObject)
                {
                    if (_serialPort.BytesToRead > 0)
                    {
                        string data = _serialPort.ReadExisting();
                        responseBuilder.Append(data);
                    }
                }

                string response = responseBuilder.ToString();
                if (response.Contains('\n') || response.Contains("END") || response.Contains("OK"))
                {
                    _logger.LogDebug("Received response: {Response}", response);
                    return ParseResponse(response);
                }

                await Task.Delay(50);
            }

            throw new TimeoutException("Terminal response timeout");
        }

        private async Task<TerminalResponse> SendNetworkCommandAsync(string command, Dictionary<string, object> parameters)
        {
            var requestData = new
            {
                command = command,
                parameters = parameters,
                merchantId = _settings.MerchantId,
                terminalId = _settings.TerminalId,
                timestamp = DateTime.UtcNow
            };

            var content = new StringContent(JsonSerializer.Serialize(requestData),
                Encoding.UTF8, "application/json");

            var response = await _httpClient.PostAsync($"{_settings.Network.BaseUrl}/api/command", content);
            var responseContent = await response.Content.ReadAsStringAsync();

            if (response.IsSuccessStatusCode)
            {
                return JsonSerializer.Deserialize<TerminalResponse>(responseContent) ?? new TerminalResponse();
            }
            else
            {
                return new TerminalResponse
                {
                    Success = false,
                    ResponseCode = ((int)response.StatusCode).ToString(),
                    ResponseMessage = $"HTTP Error: {response.StatusCode}"
                };
            }
        }

        private string BuildMessage(string command, Dictionary<string, object> parameters)
        {
            var messageBuilder = new StringBuilder();
            messageBuilder.Append($"CMD:{command}");

            foreach (var param in parameters)
            {
                messageBuilder.Append($"|{param.Key}:{param.Value}");
            }

            messageBuilder.Append("|END\n");
            return messageBuilder.ToString();
        }

        private TerminalResponse ParseResponse(string response)
        {
            var terminalResponse = new TerminalResponse();

            try
            {
                var parts = response.Split('|');
                foreach (var part in parts)
                {
                    if (part.Contains(':'))
                    {
                        var keyValue = part.Split(':', 2);
                        if (keyValue.Length == 2)
                        {
                            string key = keyValue[0].Trim();
                            string value = keyValue[1].Trim();

                            terminalResponse.RawData[key] = value;

                            switch (key.ToUpper())
                            {
                                case "RESP":
                                case "RESPONSE":
                                    terminalResponse.ResponseCode = value;
                                    terminalResponse.Success = value == "00" || value == "000";
                                    break;
                                case "MSG":
                                case "MESSAGE":
                                    terminalResponse.ResponseMessage = value;
                                    break;
                                case "TXN":
                                case "TRANSACTION":
                                    terminalResponse.TransactionId = value;
                                    break;
                                case "RECEIPT":
                                    terminalResponse.ReceiptData = value;
                                    break;
                                case "CARD":
                                    terminalResponse.CardNumber = value;
                                    break;
                                case "AUTH":
                                case "APPROVAL":
                                    terminalResponse.ApprovalCode = value;
                                    break;
                                case "AMT":
                                case "AMOUNT":
                                    if (decimal.TryParse(value, out decimal amount))
                                        terminalResponse.Amount = amount / 100;
                                    break;
                            }
                        }
                    }
                }

                if (string.IsNullOrEmpty(terminalResponse.ResponseMessage))
                {
                    terminalResponse.ResponseMessage = terminalResponse.Success ? "Success" : "Transaction failed";
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Response parsing error");
                terminalResponse.Success = false;
                terminalResponse.ResponseCode = "996";
                terminalResponse.ResponseMessage = $"Response parsing error: {ex.Message}";
            }

            return terminalResponse;
        }

        private PaymentResponse MapToPaymentResponse(TerminalResponse terminalResponse, PaymentRequest request)
        {
            return new PaymentResponse
            {
                Success = terminalResponse.Success,
                ResponseCode = terminalResponse.ResponseCode,
                ResponseMessage = terminalResponse.ResponseMessage,
                TransactionId = terminalResponse.TransactionId,
                ReceiptData = terminalResponse.ReceiptData,
                CardNumber = terminalResponse.CardNumber,
                ApprovalCode = terminalResponse.ApprovalCode,
                Amount = terminalResponse.Amount > 0 ? terminalResponse.Amount : request.Amount,
                Currency = request.Currency,
                TransactionDateTime = DateTime.Now,
                AdditionalData = terminalResponse.RawData
            };
        }

        public void Dispose()
        {
            try
            {
                if (_serialPort?.IsOpen == true)
                {
                    _serialPort.Close();
                }
                _serialPort?.Dispose();
                _httpClient?.Dispose();
                _isConnected = false;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Disposal error");
            }
        }
    }
}