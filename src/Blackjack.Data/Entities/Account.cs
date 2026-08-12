using System;

namespace Blackjack.Data.Entities
{
    /// <summary>
    /// Saldo de fichas de un jugador.
    ///
    /// Este número es un caché: la verdad está en la suma de
    /// <see cref="LedgerTransaction"/>. Se guarda aparte porque leer el libro
    /// entero en cada apuesta no escala, pero cualquier discrepancia entre
    /// ambos es un fallo que hay que investigar, no un redondeo.
    /// </summary>
    public sealed class Account
    {
        public Guid Id { get; set; } = Guid.NewGuid();

        public Guid UserId { get; set; }

        public AppUser? User { get; set; }

        public decimal Balance { get; set; }

        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;
    }
}
