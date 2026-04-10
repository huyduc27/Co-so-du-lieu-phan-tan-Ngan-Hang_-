namespace Coordinator.Api.Models
{
    public class TransferResponse
    {
        /// <summary>Mã giao dịch (GUID)</summary>
        public string TransactionId { get; set; } = string.Empty;

        /// <summary>Trạng thái: Success / Failed / Pending</summary>
        public string Status { get; set; } = string.Empty;

        /// <summary>Thông báo kết quả</summary>
        public string Message { get; set; } = string.Empty;

        /// <summary>Log chi tiết các bước 2PC đã thực hiện</summary>
        public List<string> Steps { get; set; } = new();
    }
}
