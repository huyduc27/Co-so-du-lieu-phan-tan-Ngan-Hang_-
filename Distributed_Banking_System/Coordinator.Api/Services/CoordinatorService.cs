using System.Collections.Concurrent;
using System.Net.Http.Json;
using Coordinator.Api.Models;
using Coordinator.Api.Requests;
using Coordinator.Api.Responses;
using Coordinator.Api.Data;

namespace Coordinator.Api.Services;

public class CoordinatorService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly CoordinatorData _data;

    public CoordinatorService(IHttpClientFactory httpClientFactory, CoordinatorData data)
    {
        _httpClientFactory = httpClientFactory;
        _data = data;
    }

    public ConcurrentDictionary<string, TxnRecord> GetTransactions() => _data.Transactions;

    public async Task<CoordinatorResponse> ProcessTransferAsync(TransferRequest request)
    {
        var txnId = Guid.NewGuid().ToString();
        var txn = new TxnRecord
        {
            TransactionId = txnId,
            FromAccount = request.FromAccount,
            ToAccount = request.ToAccount,
            Amount = request.Amount,
            Status = TxnState.Init
        };

        _data.Transactions.TryAdd(txnId, txn);

        var bankAClient = _httpClientFactory.CreateClient("BankA");
        var bankBClient = _httpClientFactory.CreateClient("BankB");

        try
        {
            // --- PHASE 1: PREPARE ---
            var prepareAReq = new PrepareRequest(txn.FromAccount, txn.TransactionId, txn.Amount);
            var prepareBReq = new PrepareRequest(txn.ToAccount, txn.TransactionId, txn.Amount);

            var resA = await bankAClient.PostAsJsonAsync("/Bank/prepare", prepareAReq);
            var resB = await bankBClient.PostAsJsonAsync("/Bank/prepare", prepareBReq);

            if (resA.IsSuccessStatusCode && resB.IsSuccessStatusCode)
            {
                txn.Status = TxnState.Prepared;

                // GIẢ LẬP SỰ CỐ THEO YÊU CẦU ĐỀ BÀI
                if (request.SimulateNetworkLoss)
                {
                    // Delay để giả lập đứt kết nối trước khi Commit
                    // Giao dịch sẽ treo ở trạng thái Prepared, và RecoveryWorker sẽ dọn dẹp
                    throw new Exception("Mất kết nối mạng do giả lập lỗi! Quá trình Commit đã bị ngừng tời.");
                }

                // --- PHASE 2: COMMIT ---
                var commitReq = new TransactionRequest(txn.TransactionId);
                var commitA = await bankAClient.PostAsJsonAsync("/Bank/commit", commitReq);
                var commitB = await bankBClient.PostAsJsonAsync("/Bank/commit", commitReq);

                if (commitA.IsSuccessStatusCode && commitB.IsSuccessStatusCode)
                {
                    txn.Status = TxnState.Committed;
                    return new CoordinatorResponse { Success = true, Message = $"Giao dịch {txnId} thành công!" };
                }
                else
                {
                    // If commit failed slightly (e.g. timeout), normal 2PC retries until commit succeeds.
                    // For simplicity in this demo, it marks aborted and rolls back
                    string failedBanks = "";
                    if (!commitA.IsSuccessStatusCode) failedBanks += "BankA ";
                    if (!commitB.IsSuccessStatusCode) failedBanks += "BankB";

                    txn.Status = TxnState.Aborted;
                    await RollbackBothAsync(bankAClient, bankBClient, txnId);
                    return new CoordinatorResponse { Success = false, Message = $"Giao dịch {txnId} lỗi trong khi Commit (Failed at: {failedBanks.Trim()}). Đã Rollback." };
                }
            }
            else
            {
                // Prepare fail -> Rollback
                string failedBanks = "";
                if (!resA.IsSuccessStatusCode) failedBanks += "BankA ";
                if (!resB.IsSuccessStatusCode) failedBanks += "BankB";

                txn.Status = TxnState.Aborted;
                await RollbackBothAsync(bankAClient, bankBClient, txnId);
                return new CoordinatorResponse { Success = false, Message = $"Giao dịch {txnId} thất bại tại Phase 1 (Failed at: {failedBanks.Trim()}). Đã Rollback." };
            }
        }
        catch (Exception ex)
        {
            // Gặp lỗi mạng hoặc exception -> Giao dịch đang treo, RecoveryWorker sẽ lo việc Rollback
            return new CoordinatorResponse { Success = false, Message = $"Lỗi hệ thống trong giao dịch {txnId}: {ex.Message}" };
        }
    }

    private async Task RollbackBothAsync(HttpClient bankAClient, HttpClient bankBClient, string transactionId)
    {
        var rollbackReq = new TransactionRequest(transactionId);
        try { await bankAClient.PostAsJsonAsync("/Bank/rollback", rollbackReq); } catch { /* Ignore */ }
        try { await bankBClient.PostAsJsonAsync("/Bank/rollback", rollbackReq); } catch { /* Ignore */ }
    }
}
