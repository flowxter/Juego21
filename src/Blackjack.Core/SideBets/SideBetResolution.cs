namespace Blackjack.Core.SideBets
{
    /// <summary>
    /// Resultado de una apuesta lateral.
    ///
    /// Las side bets se resuelven justo después del reparto inicial y son
    /// INDEPENDIENTES de la mano principal: puedes cobrar un par perfecto y
    /// pasarte de 21 en la misma mano. Ese es un caso que hay que testear
    /// explícitamente porque es fácil acoplarlo por error.
    /// </summary>
    public readonly struct SideBetResolution
    {
        public SideBetResolution(string category, decimal bet, decimal multiplier)
        {
            Category = category;
            Bet = bet;
            Multiplier = multiplier;
        }

        /// <summary>Nombre de la combinación premiada, o vacío si no ganó.</summary>
        public string Category { get; }

        public decimal Bet { get; }

        /// <summary>Multiplicador del pago. 25 significa 25:1. Cero si pierde.</summary>
        public decimal Multiplier { get; }

        public bool IsWin => Multiplier > 0m;

        /// <summary>
        /// Importe devuelto al saldo, apuesta incluida. Mismo convenio que
        /// <see cref="Payouts.HandResolution.Returned"/>.
        /// </summary>
        public decimal Returned => IsWin ? Bet + (Bet * Multiplier) : 0m;

        public decimal NetProfit => Returned - Bet;

        public override string ToString()
            => IsWin
                ? Category + " " + Multiplier.ToString("0.##") + ":1 · devuelto " + Returned.ToString("0.##")
                : "sin premio";
    }
}
