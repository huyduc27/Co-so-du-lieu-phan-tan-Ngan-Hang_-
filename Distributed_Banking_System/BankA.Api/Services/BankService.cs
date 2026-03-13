namespace BankA.Api.Services;

using BankA.Api.Data;
using BankA.Api.Models;
using BankA.Api.Response;

public class BankService
{
    private readonly BankData _bankData;
    private readonly Dictionary<string, Transaction> _pendingTransactions = new();

    public BankService(BankData bankData)
    {
        _bankData = bankData;
    }

    // ── Private Helpers (function) ───────────────────────────────────────

    private Account? FindAccount(string accountId)
        => _bankData.Accounts.FirstOrDefault(a => a.AccountId == accountId);

    private Transaction? FindPendingTransaction(string transactionId)
    {
        _pendingTransactions.TryGetValue(transactionId, out var tx);
        return tx?.Status == TransactionStatus.Pending ? tx : null;
    }

    private static BankResponse NotFoundAccount(string message)
        => new BankResponse { Success = false, Message = message };

    private static BankResponse Fail(string message)
        => new BankResponse { Success = false, Message = message };

    // ── Public Methods ────────────────────────────────────────

    public BankResponse GetBalance(string accountId)
    {
        var account = FindAccount(accountId);
        if (account == null) return NotFoundAccount("Account not found");

        return new BankResponse
        {
            Success = true,
            Message = "Balance retrieved successfully",
            Data = new { account.AccountId, account.OwnerName, account.Balance }
        };
    }

    public BankResponse Prepare(string accountId, string transactionId, decimal amount)
    {
        var account = FindAccount(accountId);
        if (account == null) return NotFoundAccount("Account not found");

        if (_pendingTransactions.ContainsKey(transactionId))
            return Fail("Transaction already exists");

        if (account.Balance - account.LockedAmount < amount)
            return Fail("Insufficient balance");

        account.LockedAmount += amount;
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
            Message = "Prepared successfully",
            Data = new { account.Balance, account.LockedAmount }
        };
    }

    public BankResponse Commit(string transactionId)
    {
        var tx = FindPendingTransaction(transactionId);
        if (tx == null) return Fail("Transaction not found or not pending");

        var account = FindAccount(tx.AccountId);
        if (account == null) return NotFoundAccount("Account not found");

        account.Balance -= tx.Amount;
        account.LockedAmount -= tx.Amount;
        tx.Status = TransactionStatus.Committed;

        return new BankResponse
        {
            Success = true,
            Message = "Committed successfully",
            Data = new { account.Balance, account.LockedAmount }
        };
    }

    public BankResponse Rollback(string transactionId)
    {
        var tx = FindPendingTransaction(transactionId);
        if (tx == null) return Fail("Transaction not found or not pending");

        var account = FindAccount(tx.AccountId);
        if (account == null) return NotFoundAccount("Account not found");

        account.LockedAmount -= tx.Amount;
        tx.Status = TransactionStatus.RolledBack;

        return new BankResponse
        {
            Success = true,
            Message = "Rolled back successfully",
            Data = new { account.Balance, account.LockedAmount }
        };
    }
}

