using BankB.Api.Models;

namespace BankB.Api.Repositories
{
    public interface IAccountRepository
    {
        Account? GetById(string accountId);
        void Update(Account account);
        void SaveChanges();
    }
}
