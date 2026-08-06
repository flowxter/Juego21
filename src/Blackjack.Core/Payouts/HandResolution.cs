namespace Blackjack.Core.Payouts
{
    /// <summary>
    /// Liquidación de una mano.
    ///
    /// Convenio de <see cref="Returned"/>: es el importe TOTAL que vuelve al
    /// saldo del jugador, con la apuesta original incluida. Se eligió así
    /// porque encaja con el ledger de doble entrada del servidor: la apuesta
    /// salió del saldo al hacerla, y esto es lo que entra al liquidar.
    ///
    ///   Pierde        → 0
    ///   Se rinde      → apuesta × 0.5
    ///   Empata        → apuesta
    ///   Gana          → apuesta × 2
    ///   Blackjack 3:2 → apuesta × 2.5
    /// </summary>
    public readonly struct HandResolution
    {
        public HandResolution(HandOutcome outcome, decimal bet, decimal returned)
        {
            Outcome = outcome;
            Bet = bet;
            Returned = returned;
        }

        public HandOutcome Outcome { get; }

        /// <summary>Fichas que había en la mano, ya doblada si procede.</summary>
        public decimal Bet { get; }

        /// <summary>Importe devuelto al saldo, apuesta incluida.</summary>
        public decimal Returned { get; }

        /// <summary>Ganancia neta. Negativa si perdió.</summary>
        public decimal NetProfit => Returned - Bet;

        public bool IsWin => Outcome == HandOutcome.Win || Outcome == HandOutcome.PlayerBlackjack;

        public override string ToString()
            => Outcome + " · apuesta " + Bet.ToString("0.##") + " · devuelto " + Returned.ToString("0.##");
    }
}
