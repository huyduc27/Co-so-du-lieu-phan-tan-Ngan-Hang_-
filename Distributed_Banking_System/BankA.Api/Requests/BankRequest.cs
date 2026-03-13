namespace BankA.Api.Request
{
    public class BankRequest
    {
        public record PrepareRequest(string AccountId, string TransactionId, decimal Amount);
        public record TransactionRequest(string TransactionId);
    }
}
