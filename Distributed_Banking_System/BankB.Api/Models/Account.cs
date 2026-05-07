namespace BankB.Api.Models
{
    public class Account
    {
        public string AccountId { get; set; } = string.Empty;
        public string OwnerName { get; set; } = string.Empty;
        public decimal Balance { get; set; }
        public decimal LockedAmount { get; set; }

        // Navigation property — 1 Account có nhiều Transactions
        public ICollection<Transaction> Transactions { get; set; } = new List<Transaction>();
    }
}
