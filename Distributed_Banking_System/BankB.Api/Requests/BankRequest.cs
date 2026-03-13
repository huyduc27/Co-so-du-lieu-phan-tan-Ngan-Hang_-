namespace BankB.Api.Requests
{
    public class BankRequest
    {
        public class PrepareRequest
        {
            public string TransactionId { get; set; } = string.Empty;
            public string AccountId { get; set; } = string.Empty;
            public decimal Amount { get; set; }
        }

        public class TransactionRequest
        {
            public string TransactionId { get; set; } = string.Empty;
        }
    }
}
