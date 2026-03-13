using BankB.Api.Models;

namespace BankB.Api.Data
{
    public class BankData
    {
        public List<Account> Accounts = new List<Account>()
        {
            // Bank B accounts (receiving side)
            new Account { AccountId = "B01", OwnerName = "Tran Nhan Ngoc", Balance = 2000, LockedAmount = 0 },
            new Account { AccountId = "B02", OwnerName = "Nguyen Van A", Balance = 500, LockedAmount = 0 }
        };
    }
}
