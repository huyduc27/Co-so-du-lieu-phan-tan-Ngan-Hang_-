using BankA.Api.Models;

namespace BankA.Api.Repositories
{
    public interface ITransactionRepository
    {
        Transaction? GetById(string transactionId);
        Transaction? GetPending(string transactionId);
        void Add(Transaction transaction);
        void Update(Transaction transaction);
        List<Transaction> GetAllPending();
        void SaveChanges();
    }
}
