namespace Coordinator.Api.Models;

public enum TxnState
{
    Init,
    Prepared,
    Committed,
    Aborted
}

public class TxnRecord
{
    public string TransactionId { get; set; } = Guid.NewGuid().ToString();
    public string FromBankCode { get; set; } = "";
    public string FromAccount { get; set; } = "";
    public string ToBankCode { get; set; } = "";
    public string ToAccount { get; set; } = "";
    public decimal Amount { get; set; }
    public TxnState Status { get; set; } = TxnState.Init;
    public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
}
