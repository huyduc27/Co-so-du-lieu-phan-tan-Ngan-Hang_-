using System.Collections.Concurrent;
using System.Net.Http.Json;
using Coordinator.Api.Models;
using Coordinator.Api.Requests;

namespace Coordinator.Api.Services;

public class CoordinatorService
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ConcurrentDictionary<string, TxnRecord> _transactions = new();

    public CoordinatorService(IHttpClientFactory httpClientFactory)
    {
        _httpClientFactory = httpClientFactory;
    }

    public ConcurrentDictionary<string, TxnRecord> GetTransactions() => _transactions;

    public async Task<string> ProcessTransferAsync(TransferRequest request)
    {
        if (request.FromAccount == request.ToAccount && request.FromBankCode == request.ToBankCode)
        {
            return "Lỗi: Tài khoản gửi và nhận không được trùng nhau.";
        }

        var txnId = Guid.NewGuid().ToString();
        var txn = new TxnRecord
        {
            TransactionId = txnId,
            FromBankCode = request.FromBankCode,
            FromAccount = request.FromAccount,
            ToBankCode = request.ToBankCode,
            ToAccount = request.ToAccount,
            Amount = request.Amount,
            Status = TxnState.Init
        };

        _transactions.TryAdd(txnId, txn);

        // Dynamically get clients based on BankCode
        var senderClient = _httpClientFactory.CreateClient(request.FromBankCode);
        var receiverClient = _httpClientFactory.CreateClient(request.ToBankCode);

        // Validate clients exist
        if (senderClient.BaseAddress == null || receiverClient.BaseAddress == null)
        {
            txn.Status = TxnState.Aborted;
            return $"Lỗi: BankCode không hợp lệ ({request.FromBankCode} hoặc {request.ToBankCode}).";
        }

        try
        {
            // --- PHASE 1: PREPARE ---
            var prepareSenderReq = new PrepareRequest(txn.FromAccount, txn.TransactionId, txn.Amount);
            var prepareReceiverReq = new PrepareRequest(txn.ToAccount, txn.TransactionId, txn.Amount);

            var resSender = await senderClient.PostAsJsonAsync("/Bank/prepare", prepareSenderReq);
            var resReceiver = await receiverClient.PostAsJsonAsync("/Bank/prepare", prepareReceiverReq);

            if (resSender.IsSuccessStatusCode && resReceiver.IsSuccessStatusCode)
            {
                txn.Status = TxnState.Prepared;

                // GIẢ LẬP SỰ CỐ THEO YÊU CẦU ĐỀ BÀI
                if (request.SimulateNetworkLoss)
                {
                    throw new Exception("Mất kết nối mạng do giả lập lỗi! Quá trình Commit đã bị ngừng tời.");
                }

                // --- PHASE 2: COMMIT ---
                HttpResponseMessage commitSender;
                HttpResponseMessage commitReceiver;
                var commitReq = new TransactionRequest(txn.TransactionId);

                // GIẢ LẬP LỖI PHASE 2: Fail khi gọi Commit
                if (request.SimulateCommitFailure)
                {
                    commitSender = await senderClient.PostAsJsonAsync("/Bank/api_loi", commitReq);
                    commitReceiver = await receiverClient.PostAsJsonAsync("/Bank/api_loi", commitReq);
                }
                else
                {
                    commitSender = await senderClient.PostAsJsonAsync("/Bank/commit", commitReq);
                    commitReceiver = await receiverClient.PostAsJsonAsync("/Bank/commit", commitReq);
                }

                if (commitSender.IsSuccessStatusCode && commitReceiver.IsSuccessStatusCode)
                {
                    txn.Status = TxnState.Committed;
                    return $"Giao dịch {txnId} thành công!";
                }
                else
                {
                    string failedBanks = "";
                    if (!commitSender.IsSuccessStatusCode) failedBanks += $"{request.FromBankCode} ";
                    if (!commitReceiver.IsSuccessStatusCode) failedBanks += $"{request.ToBankCode}";

                    txn.Status = TxnState.Aborted;
                    await RollbackBothAsync(senderClient, receiverClient, txnId);
                    return $"Giao dịch {txnId} lỗi trong khi Commit (Failed at: {failedBanks.Trim()}). Đã Rollback.";
                }
            }
            else
            {
                // Prepare fail -> Rollback
                string failedBanks = "";
                if (!resSender.IsSuccessStatusCode) failedBanks += $"{request.FromBankCode} ";
                if (!resReceiver.IsSuccessStatusCode) failedBanks += $"{request.ToBankCode}";

                txn.Status = TxnState.Aborted;
                await RollbackBothAsync(senderClient, receiverClient, txnId);
                return $"Giao dịch {txnId} thất bại tại Phase 1 (Failed at: {failedBanks.Trim()}). Đã Rollback.";
            }
        }
        catch (Exception ex)
        {
            // Gặp lỗi mạng -> Treo, Recovery Worker sẽ lo Rollback dựa trên cấu hình _httpClientFactory.
            return $"Lỗi hệ thống trong giao dịch {txnId}: {ex.Message}";
        }
    }

    private async Task RollbackBothAsync(HttpClient senderClient, HttpClient receiverClient, string transactionId)
    {
        var rollbackReq = new TransactionRequest(transactionId);
        try { await senderClient.PostAsJsonAsync("/Bank/rollback", rollbackReq); } catch { /* Ignore */ }
        try { await receiverClient.PostAsJsonAsync("/Bank/rollback", rollbackReq); } catch { /* Ignore */ }
    }
}
