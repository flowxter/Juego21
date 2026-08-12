using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Blackjack.Data.Entities;

namespace Blackjack.Data.Wallet
{
    /// <summary>
    /// Monedero de fichas.
    ///
    /// Es asíncrono porque la implementación real habla con PostgreSQL: hacer
    /// bloqueante una llamada a base de datos dentro del bucle de una mesa
    /// congelaría a los cinco jugadores mientras dura la consulta.
    /// </summary>
    public interface IWalletService
    {
        Task<decimal> GetBalanceAsync(Guid userId, CancellationToken ct = default);

        /// <summary>
        /// Retira fichas si el saldo alcanza. Devuelve false sin tocar nada si
        /// no llega: apostar de más es un rechazo esperable, no una excepción.
        ///
        /// La comprobación y el cargo ocurren en la misma sentencia atómica,
        /// así que dos mesas simultáneas no pueden gastar el mismo saldo.
        /// </summary>
        Task<bool> TryDebitAsync(Guid userId, decimal amount, LedgerEntryType type, string? roundId, CancellationToken ct = default);

        /// <summary>Ingresa fichas. Un importe de cero no asienta movimiento.</summary>
        Task CreditAsync(Guid userId, decimal amount, LedgerEntryType type, string? roundId, CancellationToken ct = default);

        /// <summary>Crea la cuenta con saldo inicial si aún no existe.</summary>
        Task<decimal> EnsureAccountAsync(Guid userId, CancellationToken ct = default);

        Task<IReadOnlyList<LedgerTransaction>> GetHistoryAsync(Guid userId, int limit = 50, CancellationToken ct = default);
    }
}
