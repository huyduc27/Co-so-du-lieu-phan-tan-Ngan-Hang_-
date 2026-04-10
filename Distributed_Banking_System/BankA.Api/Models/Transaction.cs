namespace BankA.Api.Models
{

    // Trạng thái của một giao dịch
    public enum TransactionStatus
    {
        Pending,
        Committed,  
        RolledBack  
    }

    // Lưu thông tin một giao dịch đang xử lý
    public class Transaction
    {
        public string TransactionId { get; set; } = "";
        public string AccountId { get; set; } = "";
        public decimal Amount { get; set; }
        public TransactionStatus Status { get; set; } = TransactionStatus.Pending;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
