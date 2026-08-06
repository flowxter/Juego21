using System;

namespace Blackjack.Core.Rounds
{
    /// <summary>
    /// Lo que un jugador pone sobre la mesa antes de repartir.
    ///
    /// Las fichas ya salieron de su saldo cuando apostó: <see cref="AvailableBalance"/>
    /// es lo que le QUEDA libre, y es lo que limita si podrá doblar o partir.
    /// El servidor lo rellena desde el ledger, nunca desde el cliente.
    /// </summary>
    public sealed class SeatBet
    {
        public SeatBet(
            int seatIndex,
            decimal mainBet,
            decimal availableBalance,
            decimal perfectPairsBet = 0m,
            decimal twentyOnePlus3Bet = 0m)
        {
            if (seatIndex < 0) throw new ArgumentOutOfRangeException(nameof(seatIndex));
            if (mainBet <= 0m) throw new ArgumentOutOfRangeException(nameof(mainBet), mainBet, "La apuesta principal debe ser positiva.");
            if (availableBalance < 0m) throw new ArgumentOutOfRangeException(nameof(availableBalance));
            if (perfectPairsBet < 0m) throw new ArgumentOutOfRangeException(nameof(perfectPairsBet));
            if (twentyOnePlus3Bet < 0m) throw new ArgumentOutOfRangeException(nameof(twentyOnePlus3Bet));

            SeatIndex = seatIndex;
            MainBet = mainBet;
            AvailableBalance = availableBalance;
            PerfectPairsBet = perfectPairsBet;
            TwentyOnePlus3Bet = twentyOnePlus3Bet;
        }

        public int SeatIndex { get; }

        public decimal MainBet { get; }

        /// <summary>Saldo libre tras haber puesto todas las apuestas de esta ronda.</summary>
        public decimal AvailableBalance { get; }

        public decimal PerfectPairsBet { get; }

        public decimal TwentyOnePlus3Bet { get; }

        public bool HasSideBets => PerfectPairsBet > 0m || TwentyOnePlus3Bet > 0m;
    }
}
