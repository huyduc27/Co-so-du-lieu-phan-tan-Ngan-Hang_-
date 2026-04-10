using BankA.Api.Models;

namespace BankA.Api.Repositories
{
    public interface IAccountRepository
    {
        Account? GetById(string accountId);
        void Update(Account account);
        void SaveChanges();
    }
}
