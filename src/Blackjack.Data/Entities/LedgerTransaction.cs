using System;

namespace Blackjack.Data.Entities
{
    public enum LedgerEntryType : byte
    {
        Deposit = 0,
        Bet = 1,
        SideBet = 2,
        Insurance = 3,
        Payout = 4,
        Refund = 5
    }

    /// <summary>
    /// Un movimiento de fichas. Solo se inserta: nunca se actualiza ni borra.
    ///
    /// Es el libro contable del juego. Permite reconstruir el saldo de
    /// cualquier jugador en cualquier momento y auditar qué pasó en una ronda
    /// concreta, que es justo lo que hace falta cuando alguien reclama que le
    /// faltan fichas.
    /// </summary>
    public sealed class LedgerTransaction
    {
        public long Id { get; set; }

        public Guid UserId { get; set; }

        public AppUser? User { get; set; }

        public LedgerEntryType Type { get; set; }

        /// <summary>Con signo: negativo al apostar, positivo al cobrar.</summary>
        public decimal Amount { get; set; }

        /// <summary>Saldo tras el movimiento. Redundante a propósito, para auditar.</summary>
        public decimal BalanceAfter { get; set; }

        /// <summary>Ronda que lo originó, si viene de una.</summary>
        public string? RoundId { get; set; }

        public DateTime CreatedUtc { get; set; } = DateTime.UtcNow;
    }
}
