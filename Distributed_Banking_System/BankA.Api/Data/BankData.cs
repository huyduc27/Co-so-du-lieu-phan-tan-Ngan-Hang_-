using BankA.Api.Models;

namespace BankA.Api.Data
{
    public class BankData
    {
        public List<Account> Accounts = new List<Account>()
        {
            new Account { AccountId = "A01", OwnerName = "Cuong Dep Trai", Balance = 1000, LockedAmount = 0 },
            new Account { AccountId = "A02", OwnerName = "Gemini AI", Balance = 5000, LockedAmount = 0 }
        };

           
    }
}
