namespace Coordinator.Api.Models
{
    public class TransferRequest
    {
        /// <summary>Mã tài khoản nguồn (bên gửi - BankA)</summary>
        public string FromAccountId { get; set; } = string.Empty;

        /// <summary>Mã tài khoản đích (bên nhận - BankB)</summary>
        public string ToAccountId { get; set; } = string.Empty;

        /// <summary>Số tiền cần chuyển</summary>
        public decimal Amount { get; set; }

        /// <summary>Bật giả lập sự cố mạng (mất kết nối sau Prepare, trước Commit)</summary>
        public bool SimulateFailure { get; set; } = false;
    }
}
