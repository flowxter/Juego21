namespace Blackjack.Protocol.Dtos
{
    /// <summary>
    /// Reglas de la mesa. El cliente las usa para rotular el fieltro, igual
    /// que una mesa real anuncia sus condiciones impresas: si el jugador tiene
    /// que adivinarlas, la mesa no es creíble.
    /// </summary>
    public sealed class TableRulesDto
    {
        public int DeckCount { get; set; }

        /// <summary>False = el croupier se planta con 17 blando (S17).</summary>
        public bool DealerHitsSoft17 { get; set; }

        /// <summary>1.5 = blackjack paga 3 a 2.</summary>
        public decimal BlackjackPayout { get; set; }

        public decimal InsurancePayout { get; set; }

        public int MaxSplits { get; set; }

        public bool DoubleAfterSplit { get; set; }

        public bool LateSurrender { get; set; }

        public decimal MinBet { get; set; }

        public decimal MaxBet { get; set; }

        public bool SideBetsEnabled { get; set; }

        public int SeatCount { get; set; }
    }
}
