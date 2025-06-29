using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using GeideaPOSIntegration.Configuration;
using GeideaPOSIntegration.Services;
using GeideaPOSIntegration.Models;

namespace GeideaPOSIntegration
{
    class Program
    {
        static async Task Main(string[] args)
        {
            // Build configuration
            var configuration = new ConfigurationBuilder()
                .SetBasePath(Directory.GetCurrentDirectory())
                .AddJsonFile("appsettings.json", optional: false, reloadOnChange: true)
                .Build();

            // Build host
            var host = Host.CreateDefaultBuilder(args)
                .ConfigureServices((context, services) =>
                {
                    services.Configure<GeideaSettings>(configuration.GetSection(GeideaSettings.SectionName));
                    services.AddSingleton<IGeideaTerminalService, GeideaTerminalService>();
                    services.AddLogging(builder =>
                    {
                        builder.AddConsole();
                        builder.SetMinimumLevel(LogLevel.Information);
                    });
                })
                .Build();

            var logger = host.Services.GetRequiredService<ILogger<Program>>();
            var terminalService = host.Services.GetRequiredService<IGeideaTerminalService>();

            try
            {
                logger.LogInformation("=== Geidea POS Terminal Integration ===");
                logger.LogInformation("Starting application...");

                // Initialize terminal
                logger.LogInformation("Initializing terminal connection...");
                bool initialized = await terminalService.InitializeAsync();

                if (!initialized)
                {
                    logger.LogError("Failed to initialize terminal connection");
                    return;
                }

                logger.LogInformation("Terminal connected successfully!");

                // Test connection
                logger.LogInformation("Testing connection...");
                bool testPassed = await terminalService.TestConnectionAsync();
                logger.LogInformation("Connection test: {Status}", testPassed ? "PASSED" : "FAILED");

                // Get terminal status
                var status = await terminalService.GetTerminalStatusAsync();
                logger.LogInformation("Terminal Status: {Status}, Ready: {Ready}",
                    status.Status, status.IsReady);

                // Interactive menu
                await RunInteractiveMenuAsync(terminalService, logger);
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Application error occurred");
            }
            finally
            {
                terminalService.Dispose();
                logger.LogInformation("Application terminated");
            }
        }

        private static async Task RunInteractiveMenuAsync(IGeideaTerminalService terminalService, ILogger logger)
        {
            while (true)
            {
                Console.WriteLine("\n=== Geidea POS Terminal Menu ===");
                Console.WriteLine("1. Process Sale");
                Console.WriteLine("2. Process Refund");
                Console.WriteLine("3. Void Transaction");
                Console.WriteLine("4. Test Connection");
                Console.WriteLine("5. Get Terminal Status");
                Console.WriteLine("6. Exit");
                Console.Write("Select option (1-6): ");

                var input = Console.ReadLine();

                switch (input)
                {
                    case "1":
                        await ProcessSaleAsync(terminalService, logger);
                        break;
                    case "2":
                        await ProcessRefundAsync(terminalService, logger);
                        break;
                    case "3":
                        await ProcessVoidAsync(terminalService, logger);
                        break;
                    case "4":
                        await TestConnectionAsync(terminalService, logger);
                        break;
                    case "5":
                        await GetTerminalStatusAsync(terminalService, logger);
                        break;
                    case "6":
                        return;
                    default:
                        Console.WriteLine("Invalid option. Please try again.");
                        break;
                }
            }
        }

        private static async Task ProcessSaleAsync(IGeideaTerminalService terminalService, ILogger logger)
        {
            try
            {
                Console.Write("Enter amount: ");
                if (!decimal.TryParse(Console.ReadLine(), out decimal amount) || amount <= 0)
                {
                    Console.WriteLine("Invalid amount");
                    return;
                }

                Console.Write("Enter description (optional): ");
                string? description = Console.ReadLine();

                var request = new PaymentRequest
                {
                    Amount = amount,
                    Currency = "SAR",
                    TransactionId = Guid.NewGuid().ToString(),
                    Description = description,
                    PaymentType = PaymentType.Sale
                };

                logger.LogInformation("Processing sale for {Amount} {Currency}", amount, "SAR");

                var response = await terminalService.ProcessPaymentAsync(request);

                Console.WriteLine($"\n=== Payment Result ===");
                Console.WriteLine($"Success: {response.Success}");
                Console.WriteLine($"Response Code: {response.ResponseCode}");
                Console.WriteLine($"Message: {response.ResponseMessage}");
                Console.WriteLine($"Transaction ID: {response.TransactionId}");

                if (response.Success)
                {
                    Console.WriteLine($"Approval Code: {response.ApprovalCode}");
                    Console.WriteLine($"Card Number: {response.CardNumber}");
                    Console.WriteLine($"Amount: {response.Amount} {response.Currency}");
                    Console.WriteLine($"Date/Time: {response.TransactionDateTime}");

                    if (!string.IsNullOrEmpty(response.ReceiptData))
                    {
                        Console.WriteLine("\n--- Receipt ---");
                        Console.WriteLine(response.ReceiptData);
                        Console.WriteLine("--- End Receipt ---");
                    }
                }
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Sale processing error");
                Console.WriteLine("An error occurred during sale processing.");
            }
        }

        private static async Task ProcessRefundAsync(IGeideaTerminalService terminalService, ILogger logger)
        {
            try
            {
                Console.Write("Enter original Transaction ID: ");
                string? originalTxnId = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(originalTxnId))
                {
                    Console.WriteLine("Transaction ID is required.");
                    return;
                }

                Console.Write("Enter refund amount: ");
                if (!decimal.TryParse(Console.ReadLine(), out decimal amount) || amount <= 0)
                {
                    Console.WriteLine("Invalid amount");
                    return;
                }

                logger.LogInformation("Processing refund for {Amount} SAR, Original Txn: {Txn}", amount, originalTxnId);
                var response = await terminalService.RefundTransactionAsync(originalTxnId, amount);

                Console.WriteLine($"\n=== Refund Result ===");
                Console.WriteLine($"Success: {response.Success}");
                Console.WriteLine($"Response Code: {response.ResponseCode}");
                Console.WriteLine($"Message: {response.ResponseMessage}");
                Console.WriteLine($"Transaction ID: {response.TransactionId}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Refund processing error");
                Console.WriteLine("An error occurred during refund processing.");
            }
        }

        private static async Task ProcessVoidAsync(IGeideaTerminalService terminalService, ILogger logger)
        {
            try
            {
                Console.Write("Enter Transaction ID to void: ");
                string? txnId = Console.ReadLine();

                if (string.IsNullOrWhiteSpace(txnId))
                {
                    Console.WriteLine("Transaction ID is required.");
                    return;
                }

                logger.LogInformation("Voiding transaction: {TxnId}", txnId);
                var response = await terminalService.VoidTransactionAsync(txnId);

                Console.WriteLine($"\n=== Void Result ===");
                Console.WriteLine($"Success: {response.Success}");
                Console.WriteLine($"Response Code: {response.ResponseCode}");
                Console.WriteLine($"Message: {response.ResponseMessage}");
                Console.WriteLine($"Transaction ID: {response.TransactionId}");
            }
            catch (Exception ex)
            {
                logger.LogError(ex, "Void processing error");
                Console.WriteLine("An error occurred during void processing.");
            }
        }

        private static async Task TestConnectionAsync(IGeideaTerminalService terminalService, ILogger logger)
        {
            logger.LogInformation("Testing terminal connection...");
            bool result = await terminalService.TestConnectionAsync();
            Console.WriteLine($"Terminal connection test: {(result ? "PASSED" : "FAILED")}");
        }

        private static async Task GetTerminalStatusAsync(IGeideaTerminalService terminalService, ILogger logger)
        {
            logger.LogInformation("Getting terminal status...");
            var status = await terminalService.GetTerminalStatusAsync();

            Console.WriteLine("\n=== Terminal Status ===");
            Console.WriteLine($"Connected: {status.IsConnected}");
            Console.WriteLine($"Ready: {status.IsReady}");
            Console.WriteLine($"Status: {status.Status}");
            Console.WriteLine($"Last Communication: {status.LastCommunication}");
            if (!string.IsNullOrEmpty(status.ErrorMessage))
            {
                Console.WriteLine($"Error: {status.ErrorMessage}");
            }
        }
    }
}
