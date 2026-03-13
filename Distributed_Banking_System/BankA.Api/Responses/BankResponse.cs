namespace BankA.Api.Response
{
    public class BankResponse
    {
        public bool Success { get; set; }
        public string Message { get; set; } = "";
        public object? Data { get; set; } // Nếu cần trả thêm data (vd: balance)
    }
}
