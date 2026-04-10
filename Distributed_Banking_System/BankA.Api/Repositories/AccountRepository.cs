using BankA.Api.Data;
using BankA.Api.Models;

namespace BankA.Api.Repositories
{
    public class AccountRepository : IAccountRepository
    {
        private readonly BankDbContext _db;

        public AccountRepository(BankDbContext db)
        {
            _db = db;
        }

        public Account? GetById(string accountId)
            => _db.Accounts.FirstOrDefault(a => a.AccountId == accountId);

        public void Update(Account account)
            => _db.Accounts.Update(account);

        public void SaveChanges()
            => _db.SaveChanges();
    }
}
