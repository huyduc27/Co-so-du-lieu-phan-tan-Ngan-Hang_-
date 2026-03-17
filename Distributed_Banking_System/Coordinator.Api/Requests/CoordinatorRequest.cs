namespace Coordinator.Api.Requests;

public record PrepareRequest(string AccountId, string TransactionId, decimal Amount);
public record TransactionRequest(string TransactionId);
public record TransferRequest(string FromAccount, string ToAccount, decimal Amount, bool SimulateNetworkLoss = false);
