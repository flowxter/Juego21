using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Blackjack.Data.Entities;
using Microsoft.EntityFrameworkCore;

namespace Blackjack.Data.Wallet
{
    /// <summary>
    /// Monedero sobre PostgreSQL.
    ///
    /// Usa <see cref="IDbContextFactory{TContext}"/> en vez de un DbContext
    /// inyectado porque quien lo llama es un actor de larga vida: un DbContext
    /// no es seguro entre hilos ni está pensado para durar horas.
    /// </summary>
    public sealed class EfWalletService : IWalletService
    {
        private readonly IDbContextFactory<BlackjackDbContext> _factory;
        private readonly decimal _startingBalance;

        public EfWalletService(IDbContextFactory<BlackjackDbContext> factory, decimal startingBalance = 1000m)
        {
            _factory = factory;
            _startingBalance = startingBalance;
        }

        public async Task<decimal> GetBalanceAsync(Guid userId, CancellationToken ct = default)
        {
            await using BlackjackDbContext db = await _factory.CreateDbContextAsync(ct);

            return await db.Accounts
                .AsNoTracking()
                .Where(a => a.UserId == userId)
                .Select(a => a.Balance)
                .FirstOrDefaultAsync(ct);
        }

        public async Task<decimal> EnsureAccountAsync(Guid userId, CancellationToken ct = default)
        {
            await using BlackjackDbContext db = await _factory.CreateDbContextAsync(ct);

            Account? account = await db.Accounts.FirstOrDefaultAsync(a => a.UserId == userId, ct);
            if (account != null) return account.Balance;

            account = new Account { UserId = userId, Balance = _startingBalance };
            db.Accounts.Add(account);
            db.LedgerTransactions.Add(new LedgerTransaction
            {
                UserId = userId,
                Type = LedgerEntryType.Deposit,
                Amount = _startingBalance,
                BalanceAfter = _startingBalance,
                RoundId = null
            });

            await db.SaveChangesAsync(ct);
            return _startingBalance;
        }

        public async Task<bool> TryDebitAsync(
            Guid userId, decimal amount, LedgerEntryType type, string? roundId, CancellationToken ct = default)
        {
            if (amount < 0m) throw new ArgumentOutOfRangeException(nameof(amount), amount, "El cargo no puede ser negativo.");
            if (amount == 0m) return true;

            await using BlackjackDbContext db = await _factory.CreateDbContextAsync(ct);
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            DateTime now = DateTime.UtcNow;

            // Comprobar y cobrar en una sola sentencia. Si se leyera el saldo
            // y luego se restara, dos mesas podrían leer el mismo saldo a la
            // vez y gastarlo dos veces.
            int updated = await db.Database.ExecuteSqlInterpolatedAsync(
                $@"UPDATE accounts
                      SET balance = balance - {amount}, updated_utc = {now}
                    WHERE user_id = {userId} AND balance >= {amount}", ct);

            if (updated == 0)
            {
                await tx.RollbackAsync(ct);
                return false;
            }

            decimal balanceAfter = await db.Accounts
                .AsNoTracking()
                .Where(a => a.UserId == userId)
                .Select(a => a.Balance)
                .SingleAsync(ct);

            db.LedgerTransactions.Add(new LedgerTransaction
            {
                UserId = userId,
                Type = type,
                Amount = -amount,
                BalanceAfter = balanceAfter,
                RoundId = roundId,
                CreatedUtc = now
            });

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
            return true;
        }

        public async Task CreditAsync(
            Guid userId, decimal amount, LedgerEntryType type, string? roundId, CancellationToken ct = default)
        {
            if (amount < 0m) throw new ArgumentOutOfRangeException(nameof(amount), amount, "El abono no puede ser negativo.");
            if (amount == 0m) return;

            await using BlackjackDbContext db = await _factory.CreateDbContextAsync(ct);
            await using var tx = await db.Database.BeginTransactionAsync(ct);

            DateTime now = DateTime.UtcNow;

            int updated = await db.Database.ExecuteSqlInterpolatedAsync(
                $@"UPDATE accounts
                      SET balance = balance + {amount}, updated_utc = {now}
                    WHERE user_id = {userId}", ct);

            if (updated == 0)
            {
                // No hay cuenta: se crea con el abono ya aplicado en vez de
                // perder el pago. Un premio nunca se descarta en silencio.
                await tx.RollbackAsync(ct);
                await EnsureAccountAsync(userId, ct);
                await CreditAsync(userId, amount, type, roundId, ct);
                return;
            }

            decimal balanceAfter = await db.Accounts
                .AsNoTracking()
                .Where(a => a.UserId == userId)
                .Select(a => a.Balance)
                .SingleAsync(ct);

            db.LedgerTransactions.Add(new LedgerTransaction
            {
                UserId = userId,
                Type = type,
                Amount = amount,
                BalanceAfter = balanceAfter,
                RoundId = roundId,
                CreatedUtc = now
            });

            await db.SaveChangesAsync(ct);
            await tx.CommitAsync(ct);
        }

        public async Task<IReadOnlyList<LedgerTransaction>> GetHistoryAsync(
            Guid userId, int limit = 50, CancellationToken ct = default)
        {
            await using BlackjackDbContext db = await _factory.CreateDbContextAsync(ct);

            return await db.LedgerTransactions
                .AsNoTracking()
                .Where(t => t.UserId == userId)
                .OrderByDescending(t => t.CreatedUtc)
                .ThenByDescending(t => t.Id)
                .Take(limit)
                .ToListAsync(ct);
        }

        /// <summary>
        /// Suma el libro entero. Si no coincide con el saldo de la cuenta hay
        /// una fuga de fichas: es la comprobación que ejecuta el test de
        /// integridad tras jugar varias rondas.
        /// </summary>
        public async Task<decimal> RecomputeBalanceAsync(Guid userId, CancellationToken ct = default)
        {
            await using BlackjackDbContext db = await _factory.CreateDbContextAsync(ct);

            return await db.LedgerTransactions
                .AsNoTracking()
                .Where(t => t.UserId == userId)
                .SumAsync(t => t.Amount, ct);
        }
    }
}
