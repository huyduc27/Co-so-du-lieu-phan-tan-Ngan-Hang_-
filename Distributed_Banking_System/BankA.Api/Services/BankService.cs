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
        if (account == null) return Fail("Không tìm thấy tài khoản");

        return new BankResponse
        {
            Success = true,
            Message = "Lấy số dư thành công",
            Data = new { account.AccountId, account.OwnerName, account.Balance }
        };
    }

    public BankResponse Prepare(string accountId, string transactionId, decimal amount)
    {
        var account = _accountRepo.GetById(accountId);
        if (account == null) return Fail("Không tìm thấy tài khoản");

        var existingTx = _transactionRepo.GetById(transactionId);
        if (existingTx != null) return Fail("Giao dịch đã tồn tại");

        if (account.Balance - account.LockedAmount < amount)
            return Fail("Số dư không đủ");

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
            Message = "Chuẩn bị thành công",
            Data = new { account.Balance, account.LockedAmount }
        };
    }

    public BankResponse Commit(string transactionId)
    {
        var tx = _transactionRepo.GetPending(transactionId);
        if (tx == null) return Fail("Không tìm thấy giao dịch hoặc trạng thái không phải chờ");

        var account = _accountRepo.GetById(tx.AccountId);
        if (account == null) return Fail("Không tìm thấy tài khoản");

        account.Balance -= tx.Amount;
        account.LockedAmount -= tx.Amount;
        tx.Status = TransactionStatus.Committed;

        _accountRepo.Update(account);
        _transactionRepo.Update(tx);
        _transactionRepo.SaveChanges();

        return new BankResponse
        {
            Success = true,
            Message = "Xác nhận thành công",
            Data = new { account.Balance, account.LockedAmount }
        };
    }

    public BankResponse Rollback(string transactionId)
    {
        var tx = _transactionRepo.GetPending(transactionId);
        if (tx == null) return Fail("Không tìm thấy giao dịch hoặc trạng thái không phải chờ");

        var account = _accountRepo.GetById(tx.AccountId);
        if (account == null) return Fail("Không tìm thấy tài khoản");

        account.LockedAmount -= tx.Amount;
        tx.Status = TransactionStatus.RolledBack;

        _accountRepo.Update(account);
        _transactionRepo.Update(tx);
        _transactionRepo.SaveChanges();

        return new BankResponse
        {
            Success = true,
            Message = "Hủy giao dịch thành công",
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
        if (tx == null) return Fail("Không tìm thấy giao dịch");
        if (tx.Status != TransactionStatus.Committed) return Fail("Giao dịch chưa được xác nhận");

        var account = _accountRepo.GetById(tx.AccountId);
        if (account == null) return Fail("Không tìm thấy tài khoản");

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
