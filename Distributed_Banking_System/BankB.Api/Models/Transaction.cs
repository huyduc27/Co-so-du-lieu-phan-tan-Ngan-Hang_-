namespace BankB.Api.Models
{
    public enum TransactionStatus { Pending, Committed, RolledBack }

    public class Transaction
    {
        public string TransactionId { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public TransactionStatus Status { get; set; }
    }
}
