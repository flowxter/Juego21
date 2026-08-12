using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Blackjack.Data.Entities;

namespace Blackjack.Data.Wallet
{
    /// <summary>
    /// Monedero en memoria. Se usa en los tests de mesa, que prueban el juego
    /// y no la base de datos, y como respaldo si el servidor arranca sin
    /// cadena de conexión configurada.
    ///
    /// Mantiene el mismo libro append-only que la versión real para que
    /// <see cref="RecomputeBalance"/> sirva igual como comprobación.
    /// </summary>
    public sealed class InMemoryWalletService : IWalletService
    {
        private readonly object _gate = new();
        private readonly Dictionary<Guid, decimal> _balances = new();
        private readonly List<LedgerTransaction> _ledger = new();
        private readonly decimal _startingBalance;
        private long _nextId = 1;

        public InMemoryWalletService(decimal startingBalance = 1000m)
        {
            _startingBalance = startingBalance;
        }

        public Task<decimal> EnsureAccountAsync(Guid userId, CancellationToken ct = default)
        {
            lock (_gate)
            {
                if (_balances.TryGetValue(userId, out decimal existing)) return Task.FromResult(existing);

                _balances[userId] = _startingBalance;
                Append(userId, LedgerEntryType.Deposit, _startingBalance, _startingBalance, null);
                return Task.FromResult(_startingBalance);
            }
        }

        public Task<decimal> GetBalanceAsync(Guid userId, CancellationToken ct = default)
        {
            lock (_gate)
            {
                return Task.FromResult(_balances.TryGetValue(userId, out decimal balance) ? balance : 0m);
            }
        }

        public Task<bool> TryDebitAsync(
            Guid userId, decimal amount, LedgerEntryType type, string? roundId, CancellationToken ct = default)
        {
            if (amount < 0m) throw new ArgumentOutOfRangeException(nameof(amount), amount, "El cargo no puede ser negativo.");
            if (amount == 0m) return Task.FromResult(true);

            lock (_gate)
            {
                decimal balance = _balances.TryGetValue(userId, out decimal b) ? b : 0m;
                if (balance < amount) return Task.FromResult(false);

                decimal after = balance - amount;
                _balances[userId] = after;
                Append(userId, type, -amount, after, roundId);
                return Task.FromResult(true);
            }
        }

        public Task CreditAsync(
            Guid userId, decimal amount, LedgerEntryType type, string? roundId, CancellationToken ct = default)
        {
            if (amount < 0m) throw new ArgumentOutOfRangeException(nameof(amount), amount, "El abono no puede ser negativo.");
            if (amount == 0m) return Task.CompletedTask;

            lock (_gate)
            {
                decimal balance = _balances.TryGetValue(userId, out decimal b) ? b : 0m;
                decimal after = balance + amount;
                _balances[userId] = after;
                Append(userId, type, amount, after, roundId);
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyList<LedgerTransaction>> GetHistoryAsync(
            Guid userId, int limit = 50, CancellationToken ct = default)
        {
            lock (_gate)
            {
                IReadOnlyList<LedgerTransaction> history = _ledger
                    .Where(e => e.UserId == userId)
                    .OrderByDescending(e => e.Id)
                    .Take(limit)
                    .ToList();

                return Task.FromResult(history);
            }
        }

        public decimal RecomputeBalance(Guid userId)
        {
            lock (_gate)
            {
                return _ledger.Where(e => e.UserId == userId).Sum(e => e.Amount);
            }
        }

        private void Append(Guid userId, LedgerEntryType type, decimal amount, decimal balanceAfter, string? roundId)
        {
            _ledger.Add(new LedgerTransaction
            {
                Id = _nextId++,
                UserId = userId,
                Type = type,
                Amount = amount,
                BalanceAfter = balanceAfter,
                RoundId = roundId,
                CreatedUtc = DateTime.UtcNow
            });
        }
    }
}
