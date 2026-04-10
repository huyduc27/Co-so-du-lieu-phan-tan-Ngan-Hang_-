using BankA.Api.Models;
using Microsoft.EntityFrameworkCore;

namespace BankA.Api.Data
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
            });

            modelBuilder.Entity<Transaction>(e =>
            {
                e.HasKey(t => t.TransactionId);
                e.Property(t => t.Amount).HasColumnType("decimal(18,2)");
            });

            modelBuilder.Entity<Account>().HasData(
                new Account { AccountId = "A001", OwnerName = "Customer A1", Balance = 100000, LockedAmount = 0 },
                new Account { AccountId = "A002", OwnerName = "Customer A2", Balance = 100000, LockedAmount = 0 }
            );
        }
    }
}
