using BankB.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BankB.Api.Data
{
    public class BankDbContext : DbContext
    {
        public BankDbContext(DbContextOptions<BankDbContext> options) : base(options) { }

        public DbSet<Account> Accounts { get; set; }
        public DbSet<Transaction> Transactions { get; set; }

        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            modelBuilder.Entity<Account>(e =>
            {
                e.HasKey(a => a.AccountId);
                e.Property(a => a.Balance).HasColumnType("decimal(18,2)");
                e.Property(a => a.LockedAmount).HasColumnType("decimal(18,2)");

                // Quan hệ 1-nhiều: 1 Account có nhiều Transactions
                e.HasMany(a => a.Transactions)
                 .WithOne(t => t.Account)
                 .HasForeignKey(t => t.AccountId)
                 .OnDelete(DeleteBehavior.Restrict); // Không xóa Account nếu còn Transaction
            });

            modelBuilder.Entity<Transaction>(e =>
            {
                e.HasKey(t => t.TransactionId);
                e.Property(t => t.Amount).HasColumnType("decimal(18,2)");
            });

            modelBuilder.Entity<Account>().HasData(
                new Account { AccountId = "B001", OwnerName = "Customer B1", Balance = 100000, LockedAmount = 0 },
                new Account { AccountId = "B002", OwnerName = "Customer B2", Balance = 100000, LockedAmount = 0 }
            );
        }
    }
}
