using GeideaPOSIntegration.Models;

namespace GeideaPOSIntegration.Services
{
    public interface IGeideaTerminalService
    {
        Task<bool> InitializeAsync();
        Task<PaymentResponse> ProcessPaymentAsync(PaymentRequest request);
        Task<PaymentResponse> RefundTransactionAsync(string originalTransactionId, decimal amount);
        Task<PaymentResponse> VoidTransactionAsync(string transactionId);
        Task<TerminalStatus> GetTerminalStatusAsync();
        Task<bool> TestConnectionAsync();
        void Dispose();
    }
}