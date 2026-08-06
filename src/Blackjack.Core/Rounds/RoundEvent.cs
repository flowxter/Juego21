using System.Collections.Generic;
using Blackjack.Core.Cards;
using Blackjack.Core.Payouts;
using Blackjack.Core.Rules;

namespace Blackjack.Core.Rounds
{
    public enum RoundEventType : byte
    {
        CardDealt = 0,
        HoleCardRevealed = 1,
        SideBetResolved = 2,
        InsuranceOffered = 3,
        InsuranceResolved = 4,
        DealerBlackjack = 5,
        TurnChanged = 6,
        HandSplit = 7,
        HandDoubled = 8,
        HandFinished = 9,
        HandSettled = 10,
        RoundComplete = 11
    }

    /// <summary>
    /// Un hecho ocurrido durante la ronda, en orden estricto.
    ///
    /// Este registro ES el protocolo: el servidor lo retransmite y el cliente
    /// Unity lo reproduce como animación. Por eso el cliente nunca necesita
    /// conocer el zapato — solo se entera de las cartas conforme se reparten,
    /// exactamente igual que un jugador sentado a la mesa.
    ///
    /// Un solo tipo con campos opcionales, en vez de una jerarquía: serializa
    /// a JSON sin polimorfismo y el cliente hace un switch sobre Type.
    /// </summary>
    public sealed class RoundEvent
    {
        /// <summary>Índice de asiento que representa al croupier.</summary>
        public const int DealerSeat = -1;

        private RoundEvent(RoundEventType type)
        {
            Type = type;
            SeatIndex = DealerSeat;
        }

        public RoundEventType Type { get; }

        /// <summary><see cref="DealerSeat"/> (-1) para el croupier.</summary>
        public int SeatIndex { get; private set; }

        public int HandIndex { get; private set; }

        public Card? Card { get; private set; }

        /// <summary>True si la carta se reparte tapada (hole card).</summary>
        public bool FaceDown { get; private set; }

        /// <summary>Importe asociado, según el tipo de evento.</summary>
        public decimal Amount { get; private set; }

        /// <summary>Texto para el cartel del cliente ("Par perfecto", "Bust").</summary>
        public string? Label { get; private set; }

        public HandOutcome? Outcome { get; private set; }

        /// <summary>Acciones ofrecidas, solo en <see cref="RoundEventType.TurnChanged"/>.</summary>
        public IReadOnlyList<PlayerAction>? LegalActions { get; private set; }

        /// <summary>
        /// Carta repartida. Si va tapada, el evento NO lleva su valor: el
        /// hueco se anuncia, la carta no. Así el registro de eventos es
        /// seguro de retransmitir tal cual, sin que el servidor tenga que
        /// acordarse de filtrar nada.
        /// </summary>
        internal static RoundEvent CardDealt(int seat, int hand, Card card, bool faceDown = false)
            => new RoundEvent(RoundEventType.CardDealt)
            {
                SeatIndex = seat,
                HandIndex = hand,
                Card = faceDown ? (Card?)null : card,
                FaceDown = faceDown
            };

        internal static RoundEvent HoleCardRevealed(Card card)
            => new RoundEvent(RoundEventType.HoleCardRevealed) { Card = card };

        internal static RoundEvent SideBetResolved(int seat, string label, decimal returned)
            => new RoundEvent(RoundEventType.SideBetResolved)
            { SeatIndex = seat, Label = label, Amount = returned };

        internal static RoundEvent InsuranceOffered()
            => new RoundEvent(RoundEventType.InsuranceOffered);

        internal static RoundEvent InsuranceResolved(int seat, decimal returned, bool won)
            => new RoundEvent(RoundEventType.InsuranceResolved)
            { SeatIndex = seat, Amount = returned, Outcome = won ? HandOutcome.Win : HandOutcome.Lose };

        internal static RoundEvent DealerBlackjack()
            => new RoundEvent(RoundEventType.DealerBlackjack);

        internal static RoundEvent TurnChanged(int seat, int hand, IReadOnlyList<PlayerAction> actions)
            => new RoundEvent(RoundEventType.TurnChanged)
            { SeatIndex = seat, HandIndex = hand, LegalActions = actions };

        internal static RoundEvent HandSplit(int seat, int hand)
            => new RoundEvent(RoundEventType.HandSplit) { SeatIndex = seat, HandIndex = hand };

        internal static RoundEvent HandDoubled(int seat, int hand, decimal newBet)
            => new RoundEvent(RoundEventType.HandDoubled)
            { SeatIndex = seat, HandIndex = hand, Amount = newBet };

        internal static RoundEvent HandFinished(int seat, int hand, string label)
            => new RoundEvent(RoundEventType.HandFinished)
            { SeatIndex = seat, HandIndex = hand, Label = label };

        internal static RoundEvent HandSettled(int seat, int hand, HandResolution resolution)
            => new RoundEvent(RoundEventType.HandSettled)
            {
                SeatIndex = seat,
                HandIndex = hand,
                Amount = resolution.Returned,
                Outcome = resolution.Outcome
            };

        internal static RoundEvent RoundComplete()
            => new RoundEvent(RoundEventType.RoundComplete);

        public override string ToString()
        {
            string who = SeatIndex == DealerSeat ? "croupier" : "asiento " + SeatIndex;
            return Type + " · " + who
                + (Card.HasValue ? " · " + Card.Value : string.Empty)
                + (Label != null ? " · " + Label : string.Empty);
        }
    }
}
