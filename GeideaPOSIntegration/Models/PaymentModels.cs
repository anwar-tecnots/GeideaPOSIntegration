using System.ComponentModel.DataAnnotations;

namespace GeideaPOSIntegration.Models
{
    public class PaymentRequest
    {
        [Required]
        [Range(0.01, double.MaxValue, ErrorMessage = "Amount must be greater than 0")]
        public decimal Amount { get; set; }

        [Required]
        [StringLength(3, MinimumLength = 3)]
        public string Currency { get; set; } = "SAR";

        [Required]
        public string TransactionId { get; set; } = Guid.NewGuid().ToString();

        public string? Description { get; set; }

        public PaymentType PaymentType { get; set; } = PaymentType.Sale;

        public Dictionary<string, string> AdditionalData { get; set; } = new();
    }

    public class PaymentResponse
    {
        public bool Success { get; set; }
        public string ResponseCode { get; set; } = string.Empty;
        public string ResponseMessage { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
        public string? ReceiptData { get; set; }
        public string? CardNumber { get; set; }
        public string? ApprovalCode { get; set; }
        public DateTime TransactionDateTime { get; set; } = DateTime.Now;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = "SAR";
        public Dictionary<string, string> AdditionalData { get; set; } = new();
    }

    public class TerminalResponse
    {
        public bool Success { get; set; }
        public string ResponseCode { get; set; } = string.Empty;
        public string ResponseMessage { get; set; } = string.Empty;
        public string TransactionId { get; set; } = string.Empty;
        public string? ReceiptData { get; set; }
        public string? CardNumber { get; set; }
        public string? ApprovalCode { get; set; }
        public decimal Amount { get; set; }
        public Dictionary<string, string> RawData { get; set; } = new();
    }

    public enum PaymentType
    {
        Sale,
        Refund,
        Void,
        PreAuth,
        Completion,
        Inquiry
    }

    public enum ConnectionType
    {
        USB,
        Serial,
        Network
    }

    public class TerminalStatus
    {
        public bool IsConnected { get; set; }
        public bool IsReady { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime LastCommunication { get; set; }
        public string? ErrorMessage { get; set; }
    }
}