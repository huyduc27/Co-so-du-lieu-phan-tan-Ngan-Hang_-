using BankB.Api.Models;
using BankB.Api.Repositories;
using BankB.Api.Responses;

namespace BankB.Api.Services
{
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

        // Phase 1: Prepare (Receiving side: check if account exists, record pending)
        public BankResponse Prepare(string accountId, string transactionId, decimal amount)
        {
            var account = _accountRepo.GetById(accountId);
            if (account == null) return Fail("Không tìm thấy tài khoản");

            var existingTx = _transactionRepo.GetById(transactionId);
            if (existingTx != null) return Fail("Giao dịch đã tồn tại");

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
                Message = "Chuẩn bị thành công. Sẵn sàng nhận tiền.",
                Data = new { account.Balance }
            };
        }

        // Phase 2: Commit (Receiving side: add money)
        public BankResponse Commit(string transactionId)
        {
            var tx = _transactionRepo.GetPending(transactionId);
            if (tx == null) return Fail("Không tìm thấy giao dịch hoặc trạng thái không phải chờ");

            var account = _accountRepo.GetById(tx.AccountId);
            if (account == null) return Fail("Không tìm thấy tài khoản");

            // Complete the transaction: add money
            account.Balance += tx.Amount;
            tx.Status = TransactionStatus.Committed;

            _accountRepo.Update(account);
            _transactionRepo.Update(tx);
            _transactionRepo.SaveChanges();

            return new BankResponse
            {
                Success = true,
                Message = "Xác nhận thành công. Đã nhận tiền.",
                Data = new { account.Balance }
            };
        }

        // Phase 2: Rollback (Receiving side: discard transaction)
        public BankResponse Rollback(string transactionId)
        {
            var tx = _transactionRepo.GetPending(transactionId);
            if (tx == null) return Fail("Không tìm thấy giao dịch hoặc trạng thái không phải chờ");

            var account = _accountRepo.GetById(tx.AccountId);
            if (account == null) return Fail("Không tìm thấy tài khoản");

            // Just discard the transaction, no balance change needed
            tx.Status = TransactionStatus.RolledBack;

            // Update transaction status
            _transactionRepo.Update(tx);
            _transactionRepo.SaveChanges();

            return new BankResponse
            {
                Success = true,
                Message = "Hủy giao dịch thành công. Giao dịch đã bị loại bỏ.",
                Data = new { account.Balance }
            };
        }

        // Lấy danh sách giao dịch đang Pending (phục vụ Recovery)
        public List<Transaction> GetPendingTransactions()
        {
            return _transactionRepo.GetAllPending();
        }
    }
}
