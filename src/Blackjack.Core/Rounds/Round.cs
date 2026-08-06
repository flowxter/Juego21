using System;
using System.Collections.Generic;
using Blackjack.Core.Cards;
using Blackjack.Core.Hands;
using Blackjack.Core.Payouts;
using Blackjack.Core.Rules;
using Blackjack.Core.Shuffling;
using Blackjack.Core.SideBets;

namespace Blackjack.Core.Rounds
{
    /// <summary>
    /// Una ronda completa de blackjack en una mesa compartida.
    ///
    /// Es la autoridad: nadie toca las cartas salvo esta clase. Los jugadores
    /// solo pueden pedir acciones, que se validan contra <see cref="ActionValidator"/>
    /// y se rechazan si no proceden. Todo lo que ocurre queda registrado en
    /// <see cref="Events"/>, que es lo que el servidor retransmite.
    ///
    /// No tiene temporizadores ni hilos: eso es responsabilidad del servidor.
    /// Aquí solo hay reglas, lo que permite jugar rondas enteras en un test
    /// en microsegundos.
    /// </summary>
    public sealed class Round
    {
        private readonly Shoe _shoe;
        private readonly TableRules _rules;
        private readonly List<Seat> _seats;
        private readonly List<RoundEvent> _events = new List<RoundEvent>(64);
        private readonly PerfectPairs _perfectPairs;
        private readonly TwentyOnePlus3 _twentyOnePlus3;

        private readonly Hand _dealerHand = new Hand();
        private bool _holeCardDealt;
        private bool _holeCardRevealed;

        public Round(
            Shoe shoe,
            TableRules rules,
            IEnumerable<SeatBet> bets,
            PerfectPairs? perfectPairs = null,
            TwentyOnePlus3? twentyOnePlus3 = null)
        {
            _shoe = shoe ?? throw new ArgumentNullException(nameof(shoe));
            _rules = rules ?? throw new ArgumentNullException(nameof(rules));
            if (bets == null) throw new ArgumentNullException(nameof(bets));

            _perfectPairs = perfectPairs ?? PerfectPairs.Standard;
            _twentyOnePlus3 = twentyOnePlus3 ?? TwentyOnePlus3.Standard;

            _seats = new List<Seat>();
            foreach (SeatBet bet in bets)
            {
                if (bet.MainBet < rules.MinBet || bet.MainBet > rules.MaxBet)
                    throw new ArgumentOutOfRangeException(nameof(bets), bet.MainBet,
                        "La apuesta del asiento " + bet.SeatIndex + " está fuera de los límites de la mesa.");

                _seats.Add(new Seat(bet));
            }

            if (_seats.Count == 0)
                throw new ArgumentException("Una ronda necesita al menos un asiento con apuesta.", nameof(bets));

            Phase = RoundPhase.NotStarted;
            CurrentSeatIndex = -1;
            CurrentHandIndex = -1;
        }

        public RoundPhase Phase { get; private set; }

        public IReadOnlyList<Seat> Seats => _seats;

        /// <summary>
        /// Mano del croupier. Cuidado: mientras <see cref="HoleCardRevealed"/>
        /// sea false contiene la carta tapada. El servidor NUNCA debe
        /// serializar esto hacia el cliente; para eso están los eventos, que
        /// omiten la carta tapada por construcción.
        /// </summary>
        public Hand DealerHand => _dealerHand;

        public Card DealerUpcard => _dealerHand.Cards[0];

        public bool HoleCardRevealed => _holeCardRevealed;

        public IReadOnlyList<RoundEvent> Events => _events;

        public int CurrentSeatIndex { get; private set; }

        public int CurrentHandIndex { get; private set; }

        public bool IsComplete => Phase == RoundPhase.Complete;

        /// <summary>Acciones ofrecidas ahora mismo, o lista vacía si no hay turno.</summary>
        public IReadOnlyList<PlayerAction> CurrentLegalActions
        {
            get
            {
                if (Phase != RoundPhase.PlayerTurns) return Array.Empty<PlayerAction>();

                Seat seat = SeatAt(CurrentSeatIndex);
                return ActionValidator.LegalActions(
                    seat.Hands[CurrentHandIndex], _rules, seat.AvailableBalance, seat.Hands.Count);
            }
        }

        /// <summary>True cuando todos los asientos respondieron al seguro.</summary>
        public bool AllInsuranceDecided
        {
            get
            {
                for (int i = 0; i < _seats.Count; i++)
                {
                    if (!_seats[i].InsuranceDecided) return false;
                }
                return true;
            }
        }

        // ------------------------------------------------------------------
        // Arranque
        // ------------------------------------------------------------------

        /// <summary>
        /// Reparte y deja la ronda en la primera fase que requiera decisión.
        /// </summary>
        public void Start()
        {
            if (Phase != RoundPhase.NotStarted)
                throw new InvalidOperationException("Esta ronda ya se repartió.");

            DealInitialCards();
            ResolveSideBets();

            // El seguro solo se ofrece con carta tapada en juego. En mesa
            // europea no hay hole card al repartir, así que lo omitimos.
            bool offerInsurance = _rules.HoleCardRule == HoleCardRule.AmericanPeek
                                  && DealerUpcard.IsAce;

            if (offerInsurance)
            {
                Phase = RoundPhase.Insurance;
                _events.Add(RoundEvent.InsuranceOffered());
                return;
            }

            for (int i = 0; i < _seats.Count; i++) _seats[i].DeclineInsurance();
            AfterInsurance();
        }

        private void DealInitialCards()
        {
            // Dos pasadas, como reparte un croupier de verdad: una carta a
            // cada jugador, luego la segunda. No es cosmético — determina qué
            // carta concreta recibe cada uno.
            for (int pass = 0; pass < 2; pass++)
            {
                for (int i = 0; i < _seats.Count; i++)
                {
                    DealToHand(_seats[i], 0);
                }

                if (pass == 0)
                {
                    Card up = _shoe.Draw();
                    _dealerHand.Add(up);
                    _events.Add(RoundEvent.CardDealt(RoundEvent.DealerSeat, 0, up));
                }
                else if (_rules.HoleCardRule == HoleCardRule.AmericanPeek)
                {
                    Card hole = _shoe.Draw();
                    _dealerHand.Add(hole);
                    _holeCardDealt = true;
                    // faceDown: el evento no lleva la carta, solo el hueco.
                    _events.Add(RoundEvent.CardDealt(RoundEvent.DealerSeat, 0, hole, faceDown: true));
                }
            }
        }

        private void ResolveSideBets()
        {
            if (!_rules.SideBetsEnabled) return;

            for (int i = 0; i < _seats.Count; i++)
            {
                Seat seat = _seats[i];
                IReadOnlyList<Card> cards = seat.Hands[0].Hand.Cards;

                if (seat.PerfectPairsBet > 0m)
                {
                    SideBetResolution r = _perfectPairs.Resolve(cards[0], cards[1], seat.PerfectPairsBet);
                    seat.PerfectPairsResult = r;
                    if (r.IsWin) _events.Add(RoundEvent.SideBetResolved(seat.Index, r.Category, r.Returned));
                }

                if (seat.TwentyOnePlus3Bet > 0m)
                {
                    SideBetResolution r = _twentyOnePlus3.Resolve(
                        cards[0], cards[1], DealerUpcard, seat.TwentyOnePlus3Bet);
                    seat.TwentyOnePlus3Result = r;
                    if (r.IsWin) _events.Add(RoundEvent.SideBetResolved(seat.Index, r.Category, r.Returned));
                }
            }
        }

        // ------------------------------------------------------------------
        // Seguro
        // ------------------------------------------------------------------

        public void TakeInsurance(int seatIndex, decimal amount)
        {
            RequirePhase(RoundPhase.Insurance);
            SeatAt(seatIndex).TakeInsurance(amount);
            if (AllInsuranceDecided) AfterInsurance();
        }

        public void DeclineInsurance(int seatIndex)
        {
            RequirePhase(RoundPhase.Insurance);
            SeatAt(seatIndex).DeclineInsurance();
            if (AllInsuranceDecided) AfterInsurance();
        }

        /// <summary>
        /// Cierra la fase de seguro rechazándolo por quien no respondió. Es lo
        /// que llama el servidor cuando expira el temporizador: la mesa no
        /// puede quedarse bloqueada por un jugador ausente.
        /// </summary>
        public void CloseInsurance()
        {
            RequirePhase(RoundPhase.Insurance);

            for (int i = 0; i < _seats.Count; i++)
            {
                if (!_seats[i].InsuranceDecided) _seats[i].DeclineInsurance();
            }

            AfterInsurance();
        }

        private void AfterInsurance()
        {
            bool dealerHasBlackjack = false;

            if (_rules.HoleCardRule == HoleCardRule.AmericanPeek
                && (DealerUpcard.IsAce || DealerUpcard.IsTenValued))
            {
                // El peek: el croupier mira la tapada. Si tiene blackjack la
                // ronda acaba aquí y los jugadores solo pierden la apuesta
                // inicial, sin haber podido doblar ni partir a ciegas.
                dealerHasBlackjack = _dealerHand.IsBlackjack;
            }

            ResolveInsuranceBets();

            if (dealerHasBlackjack)
            {
                RevealHoleCard();
                _events.Add(RoundEvent.DealerBlackjack());
                SettleAll();
                return;
            }

            Phase = RoundPhase.PlayerTurns;
            AdvanceTurn();
        }

        private void ResolveInsuranceBets()
        {
            for (int i = 0; i < _seats.Count; i++)
            {
                Seat seat = _seats[i];

                if (seat.InsuranceBet <= 0m)
                {
                    seat.InsuranceResult = new HandResolution(HandOutcome.Lose, 0m, 0m);
                    continue;
                }

                HandResolution r = PayoutCalculator.ResolveInsurance(seat.InsuranceBet, _dealerHand, _rules);
                seat.InsuranceResult = r;
                _events.Add(RoundEvent.InsuranceResolved(seat.Index, r.Returned, r.IsWin));
            }
        }

        // ------------------------------------------------------------------
        // Turnos
        // ------------------------------------------------------------------

        /// <summary>
        /// Ejecuta una acción del jugador. Lanza si llega fuera de turno o si
        /// la acción no es legal: el servidor traduce eso en "comando
        /// descartado", nunca en una corrección silenciosa.
        /// </summary>
        public void Act(int seatIndex, PlayerAction action)
        {
            RequirePhase(RoundPhase.PlayerTurns);

            if (seatIndex != CurrentSeatIndex)
                throw new InvalidOperationException(
                    "No es el turno del asiento " + seatIndex + " (juega el " + CurrentSeatIndex + ").");

            Seat seat = SeatAt(seatIndex);
            int handIndex = CurrentHandIndex;
            PlayerHand hand = seat.Hands[handIndex];

            if (!ActionValidator.IsLegal(action, hand, _rules, seat.AvailableBalance, seat.Hands.Count))
                throw new InvalidOperationException("Acción no permitida en esta mano: " + action + ".");

            switch (action)
            {
                case PlayerAction.Hit:
                    DealToHand(seat, handIndex);
                    break;

                case PlayerAction.Stand:
                    hand.Stand();
                    break;

                case PlayerAction.Double:
                    seat.CommitAdditionalBet(hand.Bet);
                    hand.Double();
                    _events.Add(RoundEvent.HandDoubled(seat.Index, handIndex, hand.Bet));
                    DealToHand(seat, handIndex);
                    break;

                case PlayerAction.Split:
                    ExecuteSplit(seat, handIndex);
                    break;

                case PlayerAction.Surrender:
                    hand.Surrender();
                    break;

                default:
                    throw new ArgumentOutOfRangeException(nameof(action), action, "Acción desconocida.");
            }

            PlayerHand finished = seat.Hands[handIndex];
            if (finished.IsFinished) EmitHandFinished(seat.Index, handIndex, finished);

            AdvanceTurn();
        }

        /// <summary>
        /// Plantarse en nombre de un jugador que agotó su tiempo. El servidor
        /// lo usa como acción por defecto: una mesa compartida no puede
        /// esperar indefinidamente a un ausente.
        /// </summary>
        public void ForceStand()
        {
            RequirePhase(RoundPhase.PlayerTurns);
            Act(CurrentSeatIndex, PlayerAction.Stand);
        }

        private void ExecuteSplit(Seat seat, int handIndex)
        {
            PlayerHand original = seat.Hands[handIndex];
            Card first = original.Hand.Cards[0];
            Card second = original.Hand.Cards[1];
            bool areAces = first.IsAce;
            int depth = original.SplitDepth + 1;

            seat.CommitAdditionalBet(original.Bet);

            // Se sustituye la mano por dos nuevas en vez de mutarla: IsFromSplit
            // es de solo lectura precisamente para que un 21 tras partir no
            // pueda confundirse nunca con un blackjack natural.
            var left = new PlayerHand(original.Bet, depth, isFromSplit: true, isSplitAces: areAces);
            var right = new PlayerHand(original.Bet, depth, isFromSplit: true, isSplitAces: areAces);

            left.Deal(first);
            right.Deal(second);

            seat.ReplaceHand(handIndex, left);
            seat.InsertHand(handIndex + 1, right);

            _events.Add(RoundEvent.HandSplit(seat.Index, handIndex));

            // Cada mano recibe su segunda carta cuando le llega el turno, que
            // es como se juega en mesa. Lo resuelve AdvanceTurn.
        }

        private void AdvanceTurn()
        {
            while (true)
            {
                if (!TryFindNextActionable(out Seat? seat, out int handIndex))
                {
                    Phase = RoundPhase.DealerPlay;
                    PlayDealer();
                    return;
                }

                PlayerHand hand = seat!.Hands[handIndex];

                // Una mano recién partida llega con una sola carta.
                if (hand.Hand.Count == 1)
                {
                    DealToHand(seat, handIndex);

                    // Los ases partidos se plantan con esa única carta.
                    if (hand.IsFinished)
                    {
                        EmitHandFinished(seat.Index, handIndex, hand);
                        continue;
                    }
                }

                CurrentSeatIndex = seat.Index;
                CurrentHandIndex = handIndex;

                _events.Add(RoundEvent.TurnChanged(
                    seat.Index,
                    handIndex,
                    ActionValidator.LegalActions(hand, _rules, seat.AvailableBalance, seat.Hands.Count)));
                return;
            }
        }

        /// <summary>
        /// Busca la primera mano pendiente recorriendo asientos y manos en
        /// orden. Se rastrea desde el principio a propósito: al partir, la
        /// mano nueva se inserta a continuación de la actual y este recorrido
        /// la encuentra en el sitio correcto sin llevar cursores aparte.
        /// </summary>
        private bool TryFindNextActionable(out Seat? seat, out int handIndex)
        {
            for (int s = 0; s < _seats.Count; s++)
            {
                IReadOnlyList<PlayerHand> hands = _seats[s].Hands;
                for (int h = 0; h < hands.Count; h++)
                {
                    if (!hands[h].IsFinished)
                    {
                        seat = _seats[s];
                        handIndex = h;
                        return true;
                    }
                }
            }

            seat = null;
            handIndex = -1;
            return false;
        }

        // ------------------------------------------------------------------
        // Croupier y liquidación
        // ------------------------------------------------------------------

        private void PlayDealer()
        {
            RevealHoleCard();

            // Si nadie sigue en pie, el croupier no roba: no tendría rival.
            // Es lo que se ve en mesa y ahorra cartas del zapato.
            if (AnyHandStillLive())
            {
                while (DealerStrategy.ShouldHit(_dealerHand, _rules))
                {
                    Card card = _shoe.Draw();
                    _dealerHand.Add(card);
                    _events.Add(RoundEvent.CardDealt(RoundEvent.DealerSeat, 0, card));
                }
            }

            SettleAll();
        }

        private bool AnyHandStillLive()
        {
            for (int s = 0; s < _seats.Count; s++)
            {
                IReadOnlyList<PlayerHand> hands = _seats[s].Hands;
                for (int h = 0; h < hands.Count; h++)
                {
                    if (!hands[h].Hand.IsBust && !hands[h].IsSurrendered) return true;
                }
            }
            return false;
        }

        private void RevealHoleCard()
        {
            if (_holeCardRevealed) return;

            if (!_holeCardDealt)
            {
                // Mesa europea: la segunda carta se reparte ahora.
                Card dealt = _shoe.Draw();
                _dealerHand.Add(dealt);
                _holeCardDealt = true;
                _holeCardRevealed = true;
                _events.Add(RoundEvent.HoleCardRevealed(dealt));
                return;
            }

            _holeCardRevealed = true;
            _events.Add(RoundEvent.HoleCardRevealed(_dealerHand.Cards[1]));
        }

        private void SettleAll()
        {
            for (int s = 0; s < _seats.Count; s++)
            {
                Seat seat = _seats[s];
                decimal total = 0m;

                for (int h = 0; h < seat.Hands.Count; h++)
                {
                    HandResolution r = PayoutCalculator.Resolve(seat.Hands[h], _dealerHand, _rules);
                    seat.AddResult(r);
                    total += r.Returned;
                    _events.Add(RoundEvent.HandSettled(seat.Index, h, r));
                }

                total += seat.InsuranceResult.Returned;
                total += seat.PerfectPairsResult.Returned;
                total += seat.TwentyOnePlus3Result.Returned;

                seat.TotalReturned = total;
            }

            Phase = RoundPhase.Complete;
            CurrentSeatIndex = -1;
            CurrentHandIndex = -1;
            _events.Add(RoundEvent.RoundComplete());
        }

        // ------------------------------------------------------------------
        // Utilidades
        // ------------------------------------------------------------------

        private void DealToHand(Seat seat, int handIndex)
        {
            Card card = _shoe.Draw();
            seat.Hands[handIndex].Deal(card);
            _events.Add(RoundEvent.CardDealt(seat.Index, handIndex, card));
        }

        private void EmitHandFinished(int seatIndex, int handIndex, PlayerHand hand)
        {
            string label = hand.IsSurrendered ? "Rendida"
                : hand.Hand.IsBust ? "Pasada"
                : hand.Hand.IsBlackjack ? "Blackjack"
                : "Plantada " + hand.Hand.Value.Total;

            _events.Add(RoundEvent.HandFinished(seatIndex, handIndex, label));
        }

        private Seat SeatAt(int seatIndex)
        {
            for (int i = 0; i < _seats.Count; i++)
            {
                if (_seats[i].Index == seatIndex) return _seats[i];
            }

            throw new ArgumentOutOfRangeException(nameof(seatIndex), seatIndex, "Ese asiento no juega esta ronda.");
        }

        private void RequirePhase(RoundPhase expected)
        {
            if (Phase != expected)
                throw new InvalidOperationException(
                    "Operación no válida en la fase " + Phase + " (se esperaba " + expected + ").");
        }
    }
}
