namespace BankA.Api.Services;
using BankA.Api.Models;
using BankA.Api.Repositories;
using BankA.Api.Response;

public class BankService
{
    private readonly IAccountRepository _accountRepo;
    private readonly ITransactionRepository _transactionRepo;

    public BankService(IAccountRepository accountRepo, ITransactionRepository transactionRepo)
    {
        _accountRepo = accountRepo;
        _transactionRepo = transactionRepo;
    }

    private static BankResponse Fail(string message)
        => new BankResponse { Success = false, Message = message };

    public BankResponse GetBalance(string accountId)
    {
        var account = _accountRepo.GetById(accountId);
        if (account == null) return Fail("Account not found");

        return new BankResponse
        {
            Success = true,
            Message = "Balance retrieved successfully",
            Data = new { account.AccountId, account.OwnerName, account.Balance }
        };
    }

    public BankResponse Prepare(string accountId, string transactionId, decimal amount)
    {
        var account = _accountRepo.GetById(accountId);
        if (account == null) return Fail("Account not found");

        var existingTx = _transactionRepo.GetById(transactionId);
        if (existingTx != null) return Fail("Transaction already exists");

        if (account.Balance - account.LockedAmount < amount)
            return Fail("Insufficient balance");

        account.LockedAmount += amount;
        _accountRepo.Update(account);

        _transactionRepo.Add(new Transaction
        {
            TransactionId = transactionId,
            AccountId = accountId,
            Amount = amount,
            Status = TransactionStatus.Pending
        });

        _transactionRepo.SaveChanges();

        return new BankResponse
        {
            Success = true,
            Message = "Prepared successfully",
            Data = new { account.Balance, account.LockedAmount }
        };
    }

    public BankResponse Commit(string transactionId)
    {
        var tx = _transactionRepo.GetPending(transactionId);
        if (tx == null) return Fail("Transaction not found or not pending");

        var account = _accountRepo.GetById(tx.AccountId);
        if (account == null) return Fail("Account not found");

        account.Balance -= tx.Amount;
        account.LockedAmount -= tx.Amount;
        tx.Status = TransactionStatus.Committed;

        _accountRepo.Update(account);
        _transactionRepo.Update(tx);
        _transactionRepo.SaveChanges();

        return new BankResponse
        {
            Success = true,
            Message = "Committed successfully",
            Data = new { account.Balance, account.LockedAmount }
        };
    }

    public BankResponse Rollback(string transactionId)
    {
        var tx = _transactionRepo.GetPending(transactionId);
        if (tx == null) return Fail("Transaction not found or not pending");

        var account = _accountRepo.GetById(tx.AccountId);
        if (account == null) return Fail("Account not found");

        account.LockedAmount -= tx.Amount;
        tx.Status = TransactionStatus.RolledBack;

        _accountRepo.Update(account);
        _transactionRepo.Update(tx);
        _transactionRepo.SaveChanges();

        return new BankResponse
        {
            Success = true,
            Message = "Rolled back successfully",
            Data = new { account.Balance, account.LockedAmount }
        };
    }

    // Lấy danh sách giao dịch đang Pending (phục vụ Recovery)
    public List<Transaction> GetPendingTransactions()
    {
        return _transactionRepo.GetAllPending();
    }

    // Hoàn tiền giao dịch đã Committed (khi BankB chưa nhận được Commit)
    public BankResponse Refund(string transactionId)
    {
        var tx = _transactionRepo.GetById(transactionId);
        if (tx == null) return Fail("Transaction not found");
        if (tx.Status != TransactionStatus.Committed) return Fail("Transaction is not in Committed state");

        var account = _accountRepo.GetById(tx.AccountId);
        if (account == null) return Fail("Account not found");

        // Hoàn tiền: cộng lại số tiền đã trừ
        account.Balance += tx.Amount;
        tx.Status = TransactionStatus.RolledBack;

        _accountRepo.Update(account);
        _transactionRepo.Update(tx);
        _transactionRepo.SaveChanges();

        return new BankResponse
        {
            Success = true,
            Message = "Refunded successfully - Đã hoàn tiền giao dịch đã commit",
            Data = new { account.Balance, account.LockedAmount }
        };
    }
}
