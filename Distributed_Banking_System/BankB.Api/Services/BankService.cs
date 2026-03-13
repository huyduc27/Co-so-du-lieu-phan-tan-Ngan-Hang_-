using BankB.Api.Data;
using BankB.Api.Models;
using BankB.Api.Responses;

namespace BankB.Api.Services
{
    public class BankService
    {
        private readonly BankData _bankData;
        private readonly Dictionary<string, Transaction> _pendingTransactions = new();

        public BankService(BankData bankData)
        {
            _bankData = bankData;
        }

        private Account? FindAccount(string accountId)
            => _bankData.Accounts.FirstOrDefault(a => a.AccountId == accountId);

        private Transaction? FindPendingTransaction(string transactionId)
        {
            _pendingTransactions.TryGetValue(transactionId, out var tx);
            return tx?.Status == TransactionStatus.Pending ? tx : null;
        }

        private static BankResponse Fail(string message)
            => new BankResponse { Success = false, Message = message };

        public BankResponse GetBalance(string accountId)
        {
            var account = FindAccount(accountId);
            if (account == null) return Fail("Account not found");

            return new BankResponse
            {
                Success = true,
                Message = "Balance retrieved successfully",
                Data = new { account.AccountId, account.OwnerName, account.Balance }
            };
        }

        // Phase 1: Prepare (Receiving side: check if account exists)
        public BankResponse Prepare(string accountId, string transactionId, decimal amount)
        {
            var account = FindAccount(accountId);
            if (account == null) return Fail("Account not found");

            if (_pendingTransactions.ContainsKey(transactionId))
                return Fail("Transaction already exists");

            // For the receiver, Prepare is essentially checking account validity
            // We record the pending transaction to process the commit later
            _pendingTransactions[transactionId] = new Transaction
            {
                TransactionId = transactionId,
                AccountId = accountId,
                Amount = amount,
                Status = TransactionStatus.Pending
            };

            return new BankResponse
            {
                Success = true,
                Message = "Prepared successfully. Ready to receive.",
                Data = new { account.Balance }
            };
        }

        // Phase 2: Commit (Receiving side: add money)
        public BankResponse Commit(string transactionId)
        {
            var tx = FindPendingTransaction(transactionId);
            if (tx == null) return Fail("Transaction not found or not pending");

            var account = FindAccount(tx.AccountId);
            if (account == null) return Fail("Account not found");

            // Complete the transaction: add money
            account.Balance += tx.Amount;
            tx.Status = TransactionStatus.Committed;

            return new BankResponse
            {
                Success = true,
                Message = "Committed successfully. Money received.",
                Data = new { account.Balance }
            };
        }

        // Phase 2: Rollback (Receiving side: discard transaction)
        public BankResponse Rollback(string transactionId)
        {
            var tx = FindPendingTransaction(transactionId);
            if (tx == null) return Fail("Transaction not found or not pending");

            var account = FindAccount(tx.AccountId);
            if (account == null) return Fail("Account not found");

            // Just discard the transaction, no balance change needed as money wasn't added yet
            tx.Status = TransactionStatus.RolledBack;

            return new BankResponse
            {
                Success = true,
                Message = "Rolled back successfully. Transaction discarded.",
                Data = new { account.Balance }
            };
        }
    }
}
