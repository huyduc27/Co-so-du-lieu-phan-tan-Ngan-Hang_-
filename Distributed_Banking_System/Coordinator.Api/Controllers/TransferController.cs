using Coordinator.Api.Models;
using Coordinator.Api.Services;
using Microsoft.AspNetCore.Mvc;

namespace Coordinator.Api.Controllers
{
    [ApiController]
    [Route("[controller]")]
    public class TransferController : ControllerBase
    {
        private readonly TransferService _transferService;
        private readonly RecoveryLogStore _logStore;

        public TransferController(TransferService transferService, RecoveryLogStore logStore)
        {
            _transferService = transferService;
            _logStore = logStore;
        }

        /// <summary>
        /// Chuyển tiền từ BankA sang BankB theo giao thức 2-Phase Commit.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> Transfer([FromBody] TransferRequest request)
        {
            if (string.IsNullOrEmpty(request.FromAccountId) || string.IsNullOrEmpty(request.ToAccountId))
                return BadRequest(new { Message = "FromAccountId và ToAccountId không được để trống" });

            if (request.Amount <= 0)
                return BadRequest(new { Message = "Số tiền phải lớn hơn 0" });

            var result = await _transferService.ExecuteTransfer(request);

            return result.Status switch
            {
                "Success" => Ok(result),
                "Pending" => Ok(result),
                _ => BadRequest(result)
            };
        }

        /// <summary>
        /// Chuyển tiền KÈM giả lập sự cố mạng.
        /// BankA sẽ trừ tiền THẬT, nhưng BankB KHÔNG được cộng tiền.
        /// Recovery Service sẽ phát hiện và hoàn tiền sau 30 giây.
        /// </summary>
        [HttpPost("simulate-failure")]
        public async Task<IActionResult> TransferWithFailure([FromBody] TransferRequest request)
        {
            if (string.IsNullOrEmpty(request.FromAccountId) || string.IsNullOrEmpty(request.ToAccountId))
                return BadRequest(new { Message = "FromAccountId và ToAccountId không được để trống" });

            if (request.Amount <= 0)
                return BadRequest(new { Message = "Số tiền phải lớn hơn 0" });

            request.SimulateFailure = true;

            var result = await _transferService.ExecuteTransfer(request);
            return Ok(result);
        }

        /// <summary>
        /// Xem lịch sử Recovery Service đã phát hiện và xử lý lỗi gì.
        /// Gọi endpoint này sau khi simulate-failure để xem Recovery đã làm gì.
        /// </summary>
        [HttpGet("recovery-logs")]
        public IActionResult GetRecoveryLogs()
        {
            var logs = _logStore.GetAll();
            return Ok(new
            {
                TotalRecoveries = logs.Count,
                Message = logs.Count == 0
                    ? "Chưa có sự cố nào được phục hồi. Nếu bạn vừa simulate-failure, hãy đợi 30 giây rồi gọi lại."
                    : $"Đã phục hồi {logs.Count} sự cố.",
                Logs = logs
            });
        }

        /// <summary>
        /// Xóa lịch sử recovery logs.
        /// </summary>
        [HttpDelete("recovery-logs")]
        public IActionResult ClearRecoveryLogs()
        {
            _logStore.Clear();
            return Ok(new { Message = "Đã xóa toàn bộ recovery logs." });
        }
    }
}
