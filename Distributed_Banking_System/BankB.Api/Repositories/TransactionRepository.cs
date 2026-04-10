using BankB.Api.Data;
using BankB.Api.Models;

namespace BankB.Api.Repositories
{
    public class TransactionRepository : ITransactionRepository
    {
        private readonly BankDbContext _db;

        public TransactionRepository(BankDbContext db)
        {
            _db = db;
        }

        public Transaction? GetById(string transactionId)
            => _db.Transactions.FirstOrDefault(t => t.TransactionId == transactionId);

        public Transaction? GetPending(string transactionId)
            => _db.Transactions.FirstOrDefault(t =>
                t.TransactionId == transactionId && t.Status == TransactionStatus.Pending);

        public void Add(Transaction transaction)
            => _db.Transactions.Add(transaction);

        public void Update(Transaction transaction)
            => _db.Transactions.Update(transaction);

        public List<Transaction> GetAllPending()
            => _db.Transactions.Where(t => t.Status == TransactionStatus.Pending).ToList();

        public void SaveChanges()
            => _db.SaveChanges();
    }
}
