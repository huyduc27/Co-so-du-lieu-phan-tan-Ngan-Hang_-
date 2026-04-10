using System.Text;
using System.Text.Json;
using Coordinator.Api.Models;

namespace Coordinator.Api.Services
{
    /// <summary>
    /// Recovery Service - Background Service chạy ngầm phát hiện và phục hồi giao dịch bị treo.
    /// Quét BankA và BankB mỗi 10 giây, tìm transaction Pending quá 30 giây,
    /// tự động gửi lệnh Rollback/Refund để hoàn tiền cho Bank A.
    /// </summary>
    public class RecoveryService : BackgroundService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<RecoveryService> _logger;
        private readonly IConfiguration _config;
        private readonly RecoveryLogStore _logStore;

        // Cấu hình
        private static readonly TimeSpan ScanInterval = TimeSpan.FromSeconds(10);
        private static readonly TimeSpan TransactionTimeout = TimeSpan.FromSeconds(30);

        public RecoveryService(
            IHttpClientFactory httpClientFactory,
            ILogger<RecoveryService> logger,
            IConfiguration config,
            RecoveryLogStore logStore)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _config = config;
            _logStore = logStore;
        }

        private string BankAUrl => _config["BankEndpoints:BankA"]!;
        private string BankBUrl => _config["BankEndpoints:BankB"]!;

        protected override async Task ExecuteAsync(CancellationToken stoppingToken)
        {
            _logger.LogInformation("🔍 Recovery Service đã khởi động. Quét mỗi {Interval}s, timeout: {Timeout}s",
                ScanInterval.TotalSeconds, TransactionTimeout.TotalSeconds);

            // Đợi một chút để các service khác khởi động
            await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken);

            while (!stoppingToken.IsCancellationRequested)
            {
                try
                {
                    await ScanAndRecoverAsync();
                }
                catch (Exception ex)
                {
                    _logger.LogError(ex, "⚠️ Recovery Service gặp lỗi khi quét");
                }

                await Task.Delay(ScanInterval, stoppingToken);
            }
        }

        private async Task ScanAndRecoverAsync()
        {
            // Quét BankA
            var bankAPending = await GetPendingTransactions(BankAUrl, "BankA");
            // Quét BankB
            var bankBPending = await GetPendingTransactions(BankBUrl, "BankB");

            var now = DateTime.UtcNow;

            // ════════════════════════════════════════
            // TRƯỜNG HỢP 1: BankA Pending + BankB Pending
            // Nghĩa là: cả 2 chưa Commit → Rollback cả 2
            // ════════════════════════════════════════
            foreach (var tx in bankAPending)
            {
                var age = now - tx.CreatedAt;
                if (age > TransactionTimeout)
                {
                    _logger.LogWarning("══════════════════════════════════════════");
                    _logger.LogWarning("🔧 RECOVERY: Phát hiện giao dịch bị treo tại BankA!");
                    _logger.LogWarning("   TransactionId: {TxId}", tx.TransactionId);
                    _logger.LogWarning("   AccountId: {AccountId} | Amount: {Amount:N0}đ", tx.AccountId, tx.Amount);
                    _logger.LogWarning("   Đã bị treo: {Age:N0} giây (timeout: {Timeout}s)", age.TotalSeconds, TransactionTimeout.TotalSeconds);
                    _logger.LogWarning("══════════════════════════════════════════");

                    // Rollback BankA (mở lock tiền)
                    _logger.LogInformation("↩️ Gửi lệnh Rollback đến BankA cho transaction {TxId}...", tx.TransactionId);
                    var rollbackA = await SendRollback(BankAUrl, tx.TransactionId);
                    if (rollbackA)
                        _logger.LogInformation("✅ BankA Rollback thành công - Tiền đã được hoàn trả cho {Account}", tx.AccountId);
                    else
                        _logger.LogError("❌ BankA Rollback thất bại cho transaction {TxId}", tx.TransactionId);

                    // Rollback BankB (hủy giao dịch pending tương ứng)
                    _logger.LogInformation("↩️ Gửi lệnh Rollback đến BankB cho transaction {TxId}...", tx.TransactionId);
                    var rollbackB = await SendRollback(BankBUrl, tx.TransactionId);
                    if (rollbackB)
                        _logger.LogInformation("✅ BankB Rollback thành công - Giao dịch đã bị hủy");
                    else
                        _logger.LogWarning("⚠️ BankB Rollback không thành công (có thể chưa có transaction tương ứng)");

                    _logger.LogInformation("🔧 RECOVERY HOÀN TẤT cho transaction {TxId}", tx.TransactionId);

                    // Ghi log vào store
                    _logStore.Add(new RecoveryLog
                    {
                        TransactionId = tx.TransactionId,
                        DetectedAt = DateTime.Now,
                        PendingSeconds = age.TotalSeconds,
                        Problem = $"Giao dịch bị treo {age.TotalSeconds:N0}s: BankA lock {tx.Amount:N0}đ từ {tx.AccountId} nhưng chưa Commit",
                        Action = "Rollback BankA (mở lock tiền) + Rollback BankB",
                        Result = rollbackA ? "✅ Thành công - Tiền đã được hoàn trả" : "❌ Thất bại"
                    });
                }
            }

            // ════════════════════════════════════════
            // TRƯỜNG HỢP 2: BankB Pending nhưng BankA KHÔNG có Pending
            // Nghĩa là: BankA đã Committed (trừ tiền thật) nhưng BankB chưa Commit
            // → Cần REFUND BankA (hoàn tiền đã trừ) + Rollback BankB
            // ════════════════════════════════════════
            foreach (var tx in bankBPending)
            {
                var age = now - tx.CreatedAt;
                if (age > TransactionTimeout)
                {
                    var existsInA = bankAPending.Any(a => a.TransactionId == tx.TransactionId);
                    if (!existsInA)
                    {
                        _logger.LogWarning("══════════════════════════════════════════");
                        _logger.LogWarning("🔧 RECOVERY: Phát hiện DỮ LIỆU SAI LỆCH!");
                        _logger.LogWarning("   TransactionId: {TxId}", tx.TransactionId);
                        _logger.LogWarning("   BankA đã Committed (trừ tiền thật), BankB vẫn Pending (chưa cộng tiền)!");
                        _logger.LogWarning("══════════════════════════════════════════");

                        // Refund BankA (cộng lại tiền đã trừ)
                        _logger.LogInformation("💰 Gửi lệnh REFUND đến BankA cho transaction {TxId}...", tx.TransactionId);
                        var refundA = await SendRefund(BankAUrl, tx.TransactionId);
                        if (refundA)
                            _logger.LogInformation("✅ BankA REFUND thành công - Đã hoàn tiền đã trừ!");
                        else
                            _logger.LogError("❌ BankA REFUND thất bại cho transaction {TxId}", tx.TransactionId);

                        // Rollback BankB
                        _logger.LogInformation("↩️ Gửi lệnh Rollback đến BankB cho transaction {TxId}...", tx.TransactionId);
                        var rollbackB = await SendRollback(BankBUrl, tx.TransactionId);
                        if (rollbackB)
                            _logger.LogInformation("✅ BankB Rollback thành công - Giao dịch đã bị hủy");

                        _logger.LogInformation("🔧 RECOVERY HOÀN TẤT - Dữ liệu đã được khôi phục!");

                        // Ghi log vào store
                        _logStore.Add(new RecoveryLog
                        {
                            TransactionId = tx.TransactionId,
                            DetectedAt = DateTime.Now,
                            PendingSeconds = age.TotalSeconds,
                            Problem = $"DỮ LIỆU SAI LỆCH: BankA đã Commit (trừ {tx.Amount:N0}đ thật), BankB vẫn Pending (chưa cộng tiền)",
                            Action = "Refund BankA (hoàn tiền đã trừ) + Rollback BankB",
                            Result = refundA ? "✅ Thành công - Đã hoàn tiền cho BankA, hủy giao dịch BankB" : "❌ Thất bại"
                        });
                    }
                }
            }
        }

        private async Task<List<PendingTransaction>> GetPendingTransactions(string bankUrl, string bankName)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("BankClient");
                var response = await client.GetAsync($"{bankUrl}/Bank/pending-transactions");

                if (!response.IsSuccessStatusCode)
                    return new List<PendingTransaction>();

                var json = await response.Content.ReadAsStringAsync();
                var transactions = JsonSerializer.Deserialize<List<PendingTransaction>>(json, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true
                });

                return transactions ?? new List<PendingTransaction>();
            }
            catch (Exception ex)
            {
                _logger.LogDebug("Không thể kết nối đến {Bank}: {Msg}", bankName, ex.Message);
                return new List<PendingTransaction>();
            }
        }

        private async Task<bool> SendRollback(string bankUrl, string transactionId)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("BankClient");
                var payload = new { TransactionId = transactionId };
                var content = new StringContent(
                    JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await client.PostAsync($"{bankUrl}/Bank/rollback", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi rollback transaction {TxId} tại {Url}", transactionId, bankUrl);
                return false;
            }
        }

        private async Task<bool> SendRefund(string bankUrl, string transactionId)
        {
            try
            {
                var client = _httpClientFactory.CreateClient("BankClient");
                var payload = new { TransactionId = transactionId };
                var content = new StringContent(
                    JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

                var response = await client.PostAsync($"{bankUrl}/Bank/refund", content);
                return response.IsSuccessStatusCode;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Lỗi khi refund transaction {TxId} tại {Url}", transactionId, bankUrl);
                return false;
            }
        }
    }

    /// <summary>DTO để deserialize transaction pending từ BankA/BankB</summary>
    public class PendingTransaction
    {
        public string TransactionId { get; set; } = string.Empty;
        public string AccountId { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public int Status { get; set; }
        public DateTime CreatedAt { get; set; }
    }
}

