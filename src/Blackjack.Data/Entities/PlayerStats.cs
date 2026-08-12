using System;

namespace Blackjack.Data.Entities
{
    /// <summary>
    /// Estadísticas agregadas del jugador.
    ///
    /// Se mantienen al día en cada liquidación en vez de recalcularse desde el
    /// historial: son las que se pintan en el perfil y en la mesa, y recorrer
    /// miles de manos para mostrar un contador no tiene sentido.
    /// </summary>
    public sealed class PlayerStats
    {
        public Guid UserId { get; set; }

        public AppUser? User { get; set; }

        public int RoundsPlayed { get; set; }

        public int HandsPlayed { get; set; }

        public int HandsWon { get; set; }

        public int HandsLost { get; set; }

        public int HandsPushed { get; set; }

        public int HandsSurrendered { get; set; }

        /// <summary>Blackjacks naturales. Los 21 tras partir no cuentan.</summary>
        public int Blackjacks { get; set; }

        public int Busts { get; set; }

        public decimal TotalWagered { get; set; }

        public decimal TotalReturned { get; set; }

        /// <summary>Mayor ganancia neta en una sola ronda.</summary>
        public decimal BiggestWin { get; set; }

        public DateTime UpdatedUtc { get; set; } = DateTime.UtcNow;

        /// <summary>
        /// Resultado neto acumulado. A la larga tenderá a ser negativo: la
        /// mesa tiene ventaja y el juego no lo disimula.
        /// </summary>
        public decimal NetResult => TotalReturned - TotalWagered;
    }
}
