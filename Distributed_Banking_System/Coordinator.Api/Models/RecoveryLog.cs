namespace Coordinator.Api.Models
{
    /// <summary>
    /// Lưu lại lịch sử Recovery Service đã làm gì
    /// </summary>
    public class RecoveryLog
    {
        public string TransactionId { get; set; } = string.Empty;
        public DateTime DetectedAt { get; set; }
        public string Problem { get; set; } = string.Empty;
        public string Action { get; set; } = string.Empty;
        public string Result { get; set; } = string.Empty;
        public double PendingSeconds { get; set; }
    }

    /// <summary>
    /// Singleton lưu trữ recovery logs trong bộ nhớ
    /// </summary>
    public class RecoveryLogStore
    {
        private readonly List<RecoveryLog> _logs = new();
        private readonly object _lock = new();

        public void Add(RecoveryLog log)
        {
            lock (_lock) { _logs.Add(log); }
        }

        public List<RecoveryLog> GetAll()
        {
            lock (_lock) { return _logs.ToList(); }
        }

        public void Clear()
        {
            lock (_lock) { _logs.Clear(); }
        }
    }
}
