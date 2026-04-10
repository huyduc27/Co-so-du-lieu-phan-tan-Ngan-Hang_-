using System.Text;
using System.Text.Json;
using Coordinator.Api.Models;

namespace Coordinator.Api.Services
{
    /// <summary>
    /// Transaction Coordinator - Điều phối giao dịch chuyển tiền theo giao thức 2PC.
    /// Phase 1 (Prepare): Hỏi cả Bank A và Bank B "Có thể thực hiện giao dịch không?"
    /// Phase 2 (Commit/Rollback): Nếu cả 2 đều OK → Commit. Nếu 1 bên fail → Rollback tất cả.
    /// </summary>
    public class TransferService
    {
        private readonly IHttpClientFactory _httpClientFactory;
        private readonly ILogger<TransferService> _logger;
        private readonly IConfiguration _config;

        public TransferService(
            IHttpClientFactory httpClientFactory,
            ILogger<TransferService> logger,
            IConfiguration config)
        {
            _httpClientFactory = httpClientFactory;
            _logger = logger;
            _config = config;
        }

        private string BankAUrl => _config["BankEndpoints:BankA"]!;
        private string BankBUrl => _config["BankEndpoints:BankB"]!;

        /// <summary>
        /// Thực hiện chuyển tiền từ BankA sang BankB theo giao thức 2-Phase Commit.
        /// </summary>
        public async Task<TransferResponse> ExecuteTransfer(TransferRequest request)
        {
            var transactionId = Guid.NewGuid().ToString();
            var response = new TransferResponse
            {
                TransactionId = transactionId,
                Status = "Pending"
            };

            var steps = response.Steps;
            bool bankAPrepared = false;
            bool bankBPrepared = false;

            try
            {
                _logger.LogInformation("══════════════════════════════════════════");
                _logger.LogInformation("🔄 Bắt đầu giao dịch 2PC: {TxId}", transactionId);
                _logger.LogInformation("   Từ: {From} → Đến: {To} | Số tiền: {Amount:N0}đ",
                    request.FromAccountId, request.ToAccountId, request.Amount);
                _logger.LogInformation("══════════════════════════════════════════");

                // ══════════════════════════════════════════
                // PHASE 0: KIỂM TRA TÀI KHOẢN TỒN TẠI
                // ══════════════════════════════════════════
                steps.Add("══ PHASE 0: KIỂM TRA TÀI KHOẢN ══");

                // 0a. Kiểm tra tài khoản nguồn (BankA)
                steps.Add($"[BankA] Kiểm tra tài khoản {request.FromAccountId}...");
                _logger.LogInformation("🔍 Kiểm tra tài khoản nguồn {Account} tại BankA...", request.FromAccountId);

                var checkA = await CheckAccountExists(BankAUrl, request.FromAccountId);
                if (!checkA.Success)
                {
                    steps.Add($"[BankA] ❌ Tài khoản {request.FromAccountId} KHÔNG TỒN TẠI");
                    _logger.LogWarning("❌ Tài khoản nguồn {Account} không tồn tại!", request.FromAccountId);

                    response.Status = "Failed";
                    response.Message = $"Tài khoản nguồn {request.FromAccountId} không tồn tại tại BankA.";
                    return response;
                }
                steps.Add($"[BankA] ✅ Tài khoản {request.FromAccountId} tồn tại");

                // 0b. Kiểm tra tài khoản đích (BankB)
                steps.Add($"[BankB] Kiểm tra tài khoản {request.ToAccountId}...");
                _logger.LogInformation("🔍 Kiểm tra tài khoản đích {Account} tại BankB...", request.ToAccountId);

                var checkB = await CheckAccountExists(BankBUrl, request.ToAccountId);
                if (!checkB.Success)
                {
                    steps.Add($"[BankB] ❌ Tài khoản {request.ToAccountId} KHÔNG TỒN TẠI");
                    _logger.LogWarning("❌ Tài khoản đích {Account} không tồn tại!", request.ToAccountId);

                    response.Status = "Failed";
                    response.Message = $"Tài khoản đích {request.ToAccountId} không tồn tại tại BankB.";
                    return response;
                }
                steps.Add($"[BankB] ✅ Tài khoản {request.ToAccountId} tồn tại");

                steps.Add("══ CẢ 2 TÀI KHOẢN HỢP LỆ → BẮT ĐẦU 2PC ══");
                _logger.LogInformation("✅ Cả 2 tài khoản đều tồn tại → Bắt đầu 2PC");

                // ══════════════════════════════════════════
                // PHASE 1: PREPARE - Hỏi cả 2 Bank
                // ══════════════════════════════════════════
                steps.Add("══ PHASE 1: PREPARE ══");

                // 1a. Prepare BankA (bên gửi - trừ tiền, lock số dư)
                steps.Add($"[BankA] Gửi lệnh Prepare: Lock {request.Amount:N0}đ từ tài khoản {request.FromAccountId}...");
                _logger.LogInformation("📤 Phase 1: Gửi Prepare đến BankA ({Account})...", request.FromAccountId);

                var prepareAResult = await SendPrepare(BankAUrl, request.FromAccountId, transactionId, request.Amount);

                if (!prepareAResult.Success)
                {
                    steps.Add($"[BankA] ❌ Prepare THẤT BẠI: {prepareAResult.Message}");
                    _logger.LogWarning("❌ BankA Prepare thất bại: {Msg}", prepareAResult.Message);

                    response.Status = "Failed";
                    response.Message = $"BankA Prepare thất bại: {prepareAResult.Message}";
                    return response;
                }

                bankAPrepared = true;
                steps.Add($"[BankA] ✅ Prepare THÀNH CÔNG - Đã lock {request.Amount:N0}đ");
                _logger.LogInformation("✅ BankA Prepare thành công");

                // 1b. Prepare BankB (bên nhận - sẵn sàng nhận tiền)
                steps.Add($"[BankB] Gửi lệnh Prepare: Chuẩn bị nhận {request.Amount:N0}đ vào tài khoản {request.ToAccountId}...");
                _logger.LogInformation("📤 Phase 1: Gửi Prepare đến BankB ({Account})...", request.ToAccountId);

                var prepareBResult = await SendPrepare(BankBUrl, request.ToAccountId, transactionId, request.Amount);

                if (!prepareBResult.Success)
                {
                    steps.Add($"[BankB] ❌ Prepare THẤT BẠI: {prepareBResult.Message}");
                    _logger.LogWarning("❌ BankB Prepare thất bại: {Msg}", prepareBResult.Message);

                    // BankA đã Prepare thành công → phải Rollback
                    steps.Add("[BankA] ↩️ Gửi lệnh Rollback vì BankB từ chối...");
                    _logger.LogInformation("↩️ Rollback BankA vì BankB từ chối...");
                    await SendRollback(BankAUrl, transactionId);
                    steps.Add("[BankA] ✅ Rollback thành công - Tiền đã được hoàn trả");

                    response.Status = "Failed";
                    response.Message = $"BankB Prepare thất bại: {prepareBResult.Message}. Đã rollback BankA.";
                    return response;
                }

                bankBPrepared = true;
                steps.Add($"[BankB] ✅ Prepare THÀNH CÔNG - Sẵn sàng nhận {request.Amount:N0}đ");
                _logger.LogInformation("✅ BankB Prepare thành công");

                steps.Add("══ CẢ 2 BANK ĐỀU SẴN SÀNG ══");
                _logger.LogInformation("🎯 Cả 2 Bank đều Prepare thành công!");

                // ══════════════════════════════════════════
                // PHASE 2: COMMIT - Xác nhận cả 2 bên
                // ══════════════════════════════════════════
                steps.Add("══ PHASE 2: COMMIT ══");

                // 2a. Commit BankA (xác nhận trừ tiền)
                steps.Add("[BankA] Gửi lệnh Commit: Xác nhận trừ tiền...");
                _logger.LogInformation("📤 Phase 2: Gửi Commit đến BankA...");

                var commitAResult = await SendCommit(BankAUrl, transactionId);
                if (!commitAResult.Success)
                {
                    steps.Add($"[BankA] ❌ Commit THẤT BẠI: {commitAResult.Message}");
                    _logger.LogError("❌ BankA Commit thất bại!");

                    // Rollback cả 2
                    steps.Add("[BankA] ↩️ Rollback...");
                    steps.Add("[BankB] ↩️ Rollback...");
                    await SendRollback(BankAUrl, transactionId);
                    await SendRollback(BankBUrl, transactionId);
                    steps.Add("[Cả 2 Bank] ✅ Đã rollback thành công");

                    response.Status = "Failed";
                    response.Message = $"BankA Commit thất bại. Đã rollback cả 2 bên.";
                    return response;
                }

                steps.Add("[BankA] ✅ Commit THÀNH CÔNG - Đã trừ tiền");
                _logger.LogInformation("✅ BankA Commit thành công - Tiền đã bị trừ khỏi tài khoản!");

                // ══════════════════════════════════════════
                // GIẢ LẬP SỰ CỐ MẠNG: Sau khi BankA trừ tiền, trước khi BankB cộng tiền
                // ══════════════════════════════════════════
                if (request.SimulateFailure)
                {
                    steps.Add("══ 💥 GIẢ LẬP SỰ CỐ MẠNG 💥 ══");
                    steps.Add("[BankA] Đã trừ tiền THẬT → balance giảm!");
                    steps.Add("[BankB] Chưa cộng tiền → balance KHÔNG đổi!");
                    _logger.LogError("💥 GIẢ LẬP SỰ CỐ MẠNG! Transaction {TxId}", transactionId);
                    _logger.LogError("   BankA ĐÃ TRỪ TIỀN, nhưng BankB CHƯA CỘNG TIỀN!");

                    // ── SLEEP: Giả lập độ trễ mạng trước khi đứt kết nối ──
                    _logger.LogError("⏳ Sleep 5 giây - giả lập mạng chậm dần...");
                    steps.Add("⏳ Sleep 5 giây - giả lập mạng chậm dần...");
                    Thread.Sleep(5000);  // ← SLEEP: Dừng 5 giây

                    // ── BREAK: Ngắt kết nối - BankB Commit không được gửi ──
                    _logger.LogError("💥 BREAK: Mất kết nối! Lệnh Commit đến BankB KHÔNG được gửi!");
                    steps.Add("💥 BREAK: Mất kết nối! Lệnh Commit đến BankB KHÔNG được gửi!");
                    steps.Add("💥 Dữ liệu SAI LỆCH: BankA đã trừ tiền, BankB chưa cộng tiền!");
                    steps.Add("💥 Chờ Recovery Service phát hiện và hoàn tiền cho BankA...");

                    response.Status = "Pending";
                    response.Message = "GIẢ LẬP SỰ CỐ: BankA ĐÃ TRỪ TIỀN nhưng BankB CHƯA CỘNG TIỀN. " +
                                       "Dữ liệu bị sai lệch! Recovery Service sẽ phát hiện và hoàn tiền cho BankA.";
                    return response;  // ← BREAK: return luôn, BankB Commit không bao giờ được gửi
                }

                // 2b. Commit BankB (xác nhận cộng tiền)
                steps.Add("[BankB] Gửi lệnh Commit: Xác nhận cộng tiền...");
                _logger.LogInformation("📤 Phase 2: Gửi Commit đến BankB...");

                var commitBResult = await SendCommit(BankBUrl, transactionId);
                if (!commitBResult.Success)
                {
                    steps.Add($"[BankB] ❌ Commit THẤT BẠI: {commitBResult.Message}");
                    _logger.LogError("❌ BankB Commit thất bại!");

                    // BankA đã commit rồi, BankB thất bại → Rollback BankB
                    steps.Add("[BankB] ↩️ Rollback...");
                    await SendRollback(BankBUrl, transactionId);

                    response.Status = "Failed";
                    response.Message = $"BankB Commit thất bại. Giao dịch không hoàn tất.";
                    return response;
                }

                steps.Add("[BankB] ✅ Commit THÀNH CÔNG - Đã cộng tiền");
                _logger.LogInformation("✅ BankB Commit thành công");

                // ══════════════════════════════════════════
                // HOÀN TẤT
                // ══════════════════════════════════════════
                steps.Add("══ GIAO DỊCH HOÀN TẤT ══");
                steps.Add($"✅ Đã chuyển {request.Amount:N0}đ từ {request.FromAccountId} (BankA) → {request.ToAccountId} (BankB)");

                response.Status = "Success";
                response.Message = $"Chuyển tiền thành công! {request.Amount:N0}đ từ {request.FromAccountId} → {request.ToAccountId}";

                _logger.LogInformation("══════════════════════════════════════════");
                _logger.LogInformation("✅ GIAO DỊCH HOÀN TẤT: {TxId}", transactionId);
                _logger.LogInformation("══════════════════════════════════════════");

                return response;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "💥 Lỗi không mong muốn trong giao dịch {TxId}", transactionId);
                steps.Add($"💥 LỖI: {ex.Message}");

                // Rollback các Bank đã Prepare
                if (bankAPrepared)
                {
                    steps.Add("[BankA] ↩️ Rollback do lỗi hệ thống...");
                    try { await SendRollback(BankAUrl, transactionId); }
                    catch { steps.Add("[BankA] ⚠️ Rollback thất bại!"); }
                }
                if (bankBPrepared)
                {
                    steps.Add("[BankB] ↩️ Rollback do lỗi hệ thống...");
                    try { await SendRollback(BankBUrl, transactionId); }
                    catch { steps.Add("[BankB] ⚠️ Rollback thất bại!"); }
                }

                response.Status = "Failed";
                response.Message = $"Lỗi hệ thống: {ex.Message}";
                return response;
            }
        }

        // ═══════════════════════════════════════════════════
        // Helper Methods: Gọi API tới BankA / BankB
        // ═══════════════════════════════════════════════════

        private async Task<BankApiResponse> CheckAccountExists(string bankUrl, string accountId)
        {
            var client = _httpClientFactory.CreateClient("BankClient");
            var httpResponse = await client.GetAsync($"{bankUrl}/Bank/Balance/{accountId}");
            var json = await httpResponse.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<BankApiResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new BankApiResponse { Success = false, Message = "Không thể đọc phản hồi từ Bank" };
        }

        private async Task<BankApiResponse> SendPrepare(string bankUrl, string accountId, string transactionId, decimal amount)
        {
            var client = _httpClientFactory.CreateClient("BankClient");
            var payload = new { AccountId = accountId, TransactionId = transactionId, Amount = amount };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var httpResponse = await client.PostAsync($"{bankUrl}/Bank/prepare", content);
            var json = await httpResponse.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<BankApiResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new BankApiResponse { Success = false, Message = "Không thể đọc phản hồi từ Bank" };
        }

        private async Task<BankApiResponse> SendCommit(string bankUrl, string transactionId)
        {
            var client = _httpClientFactory.CreateClient("BankClient");
            var payload = new { TransactionId = transactionId };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var httpResponse = await client.PostAsync($"{bankUrl}/Bank/commit", content);
            var json = await httpResponse.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<BankApiResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new BankApiResponse { Success = false, Message = "Không thể đọc phản hồi từ Bank" };
        }

        private async Task<BankApiResponse> SendRollback(string bankUrl, string transactionId)
        {
            var client = _httpClientFactory.CreateClient("BankClient");
            var payload = new { TransactionId = transactionId };
            var content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            var httpResponse = await client.PostAsync($"{bankUrl}/Bank/rollback", content);
            var json = await httpResponse.Content.ReadAsStringAsync();

            return JsonSerializer.Deserialize<BankApiResponse>(json, new JsonSerializerOptions
            {
                PropertyNameCaseInsensitive = true
            }) ?? new BankApiResponse { Success = false, Message = "Không thể đọc phản hồi từ Bank" };
        }
    }

    /// <summary>DTO để deserialize phản hồi từ BankA/BankB API</summary>
    public class BankApiResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public object? Data { get; set; }
    }
}
