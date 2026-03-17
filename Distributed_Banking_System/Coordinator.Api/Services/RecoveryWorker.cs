using System.Net.Http.Json;
using Coordinator.Api.Models;
using Coordinator.Api.Requests;
using Coordinator.Api.Services;

namespace Coordinator.Api.Workers;

public class RecoveryWorker : BackgroundService
{
    private readonly CoordinatorService _coordinatorService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<RecoveryWorker> _logger;

    public RecoveryWorker(CoordinatorService coordinatorService, IHttpClientFactory httpClientFactory, ILogger<RecoveryWorker> logger)
    {
        _coordinatorService = coordinatorService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            var txns = _coordinatorService.GetTransactions();
            var bankAClient = _httpClientFactory.CreateClient("BankA");
            var bankBClient = _httpClientFactory.CreateClient("BankB");

            // Tìm các giao dịch nào đang ở trạng thái Prepared quá 15 giây (bị kẹt/treo)
            var stuckTxns = txns.Values
                .Where(t => t.Status == TxnState.Prepared && (DateTime.UtcNow - t.CreatedAt).TotalSeconds > 15)
                .ToList();

            foreach (var txn in stuckTxns)
            {
                _logger.LogWarning($"Phát hiện giao dịch {txn.TransactionId} bị treo quá lâu. Đang Phục Hồi (Rollback)...");
                
                var rollbackReq = new TransactionRequest(txn.TransactionId);

                try
                {
                    // Rollback Bank A (Hoàn tiền)
                    await bankAClient.PostAsJsonAsync("/Bank/rollback", rollbackReq);
                    // Rollback Bank B (Hủy giao dịch bên nhận)
                    await bankBClient.PostAsJsonAsync("/Bank/rollback", rollbackReq);

                    txn.Status = TxnState.Aborted;
                    _logger.LogInformation($"Giao dịch {txn.TransactionId} đã phục hồi thành công (Hoàn tiền cho Bank A).");
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, $"Lỗi xảy ra trong quá trình phục hồi {txn.TransactionId}");
                }
            }

            await Task.Delay(5000, stoppingToken); // Chạy kiểm tra mỗi 5s
        }
    }
}
