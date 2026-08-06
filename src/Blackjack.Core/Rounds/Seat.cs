using System;
using System.Collections.Generic;
using Blackjack.Core.Hands;
using Blackjack.Core.Payouts;
using Blackjack.Core.SideBets;

namespace Blackjack.Core.Rounds
{
    /// <summary>
    /// Un asiento durante la ronda: sus manos, su seguro y sus resultados.
    /// Un asiento empieza con una mano y puede acabar con cuatro si parte.
    /// </summary>
    public sealed class Seat
    {
        private readonly List<PlayerHand> _hands = new List<PlayerHand>(4);
        private readonly List<HandResolution> _results = new List<HandResolution>(4);

        internal Seat(SeatBet bet)
        {
            Index = bet.SeatIndex;
            InitialBet = bet.MainBet;
            AvailableBalance = bet.AvailableBalance;
            PerfectPairsBet = bet.PerfectPairsBet;
            TwentyOnePlus3Bet = bet.TwentyOnePlus3Bet;

            _hands.Add(new PlayerHand(bet.MainBet));
        }

        public int Index { get; }

        public decimal InitialBet { get; }

        /// <summary>
        /// Saldo libre. Baja al doblar o partir, porque esas acciones exigen
        /// poner más fichas sobre la mesa.
        /// </summary>
        public decimal AvailableBalance { get; private set; }

        public IReadOnlyList<PlayerHand> Hands => _hands;

        public decimal PerfectPairsBet { get; }

        public decimal TwentyOnePlus3Bet { get; }

        public SideBetResolution PerfectPairsResult { get; internal set; }

        public SideBetResolution TwentyOnePlus3Result { get; internal set; }

        public decimal InsuranceBet { get; private set; }

        /// <summary>
        /// True cuando el asiento ya respondió al seguro (aceptando o
        /// rechazando). La ronda no avanza hasta que todos hayan decidido o
        /// se agote el temporizador.
        /// </summary>
        public bool InsuranceDecided { get; private set; }

        public HandResolution InsuranceResult { get; internal set; }

        public IReadOnlyList<HandResolution> Results => _results;

        /// <summary>
        /// Total devuelto al saldo al liquidar: manos, seguro y side bets.
        /// Es el importe que el servidor asienta en el ledger.
        /// </summary>
        public decimal TotalReturned { get; internal set; }

        internal void TakeInsurance(decimal amount)
        {
            if (InsuranceDecided) throw new InvalidOperationException("Este asiento ya decidió sobre el seguro.");

            decimal max = PayoutCalculator.MaxInsuranceBet(InitialBet);
            if (amount < 0m || amount > max)
                throw new ArgumentOutOfRangeException(nameof(amount), amount,
                    "El seguro no puede superar la mitad de la apuesta principal (" + max.ToString("0.##") + ").");
            if (amount > AvailableBalance)
                throw new InvalidOperationException("Saldo insuficiente para cubrir el seguro.");

            InsuranceBet = amount;
            AvailableBalance -= amount;
            InsuranceDecided = true;
        }

        internal void DeclineInsurance() => InsuranceDecided = true;

        /// <summary>Retira fichas del saldo libre al doblar o partir.</summary>
        internal void CommitAdditionalBet(decimal amount)
        {
            if (amount > AvailableBalance)
                throw new InvalidOperationException("Saldo insuficiente para esta acción.");

            AvailableBalance -= amount;
        }

        internal void ReplaceHand(int index, PlayerHand hand) => _hands[index] = hand;

        internal void InsertHand(int index, PlayerHand hand) => _hands.Insert(index, hand);

        internal void AddResult(HandResolution resolution) => _results.Add(resolution);
    }
}
