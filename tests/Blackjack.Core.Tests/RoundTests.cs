using System;
using System.Linq;
using Blackjack.Core.Payouts;
using Blackjack.Core.Rounds;
using Blackjack.Core.Rules;
using Blackjack.Core.Shuffling;
using Xunit;
using static Blackjack.Core.Tests.TestCards;

namespace Blackjack.Core.Tests
{
    public class RoundTests
    {
        private static readonly TableRules Vegas = TableRules.VegasStrip;

        private static Round UnAsiento(Shoe shoe, decimal apuesta = 100m, decimal saldo = 1000m, TableRules? reglas = null)
            => new Round(shoe, reglas ?? Vegas, new[] { new SeatBet(0, apuesta, saldo) });

        // ------------------------------------------------------------------
        // Reparto
        // ------------------------------------------------------------------

        [Fact]
        public void ElRepartoVaEnDosPasadas_NoDosCartasSeguidasAlMismoJugador()
        {
            // Orden esperado: s0, s1, croupier, s0, s1, croupier(tapada).
            Shoe shoe = StackedShoe("2c", "3c", "4c", "5c", "6c", "7c", "8c", "9c");
            var round = new Round(shoe, Vegas, new[]
            {
                new SeatBet(0, 100m, 1000m),
                new SeatBet(1, 100m, 1000m)
            });

            round.Start();

            Assert.Equal(Many("2c", "5c"), round.Seats[0].Hands[0].Hand.Cards.ToArray());
            Assert.Equal(Many("3c", "6c"), round.Seats[1].Hands[0].Hand.Cards.ToArray());
            Assert.Equal(C("4c"), round.DealerUpcard);
        }

        [Fact]
        public void LaCartaTapada_NoViajaEnLosEventos()
        {
            // Si esto falla, el cliente puede leer la hole card y el juego
            // deja de tener sentido.
            Shoe shoe = StackedShoe("Ts", "6d", "9h", "Kc", "5s");
            Round round = UnAsiento(shoe);

            round.Start();

            RoundEvent tapada = round.Events.Single(e => e.Type == RoundEventType.CardDealt && e.FaceDown);

            Assert.Null(tapada.Card);
            Assert.Equal(RoundEvent.DealerSeat, tapada.SeatIndex);
        }

        [Fact]
        public void AlDestapar_LaCartaSiApareceEnElEvento()
        {
            Shoe shoe = StackedShoe("Ts", "6d", "9h", "Kc", "5s");
            Round round = UnAsiento(shoe);
            round.Start();

            round.Act(0, PlayerAction.Stand);

            RoundEvent revelada = round.Events.Single(e => e.Type == RoundEventType.HoleCardRevealed);
            Assert.Equal(C("Kc"), revelada.Card);
        }

        // ------------------------------------------------------------------
        // Splits
        // ------------------------------------------------------------------

        [Fact]
        public void PartirOchos_CreaDosManosYCobraLaApuestaExtra()
        {
            // s0: 8s 8h | croupier: 6d Tc (16) | luego 3c, 4h, 9s
            Shoe shoe = StackedShoe("8s", "6d", "8h", "Tc", "3c", "4h", "9s");
            Round round = UnAsiento(shoe, apuesta: 100m, saldo: 1000m);
            round.Start();

            Assert.Contains(PlayerAction.Split, round.CurrentLegalActions);

            round.Act(0, PlayerAction.Split);

            Seat seat = round.Seats[0];
            Assert.Equal(2, seat.Hands.Count);
            Assert.Equal(900m, seat.AvailableBalance); // se comprometieron otras 100
            Assert.All(seat.Hands, h => Assert.True(h.Hand.IsFromSplit));

            // La primera mano recibe su segunda carta y toma el turno.
            Assert.Equal(0, round.CurrentHandIndex);
            Assert.Equal(Many("8s", "3c"), seat.Hands[0].Hand.Cards.ToArray());

            round.Act(0, PlayerAction.Stand);

            // Ahora le toca a la segunda, que recibe la suya.
            Assert.Equal(1, round.CurrentHandIndex);
            Assert.Equal(Many("8h", "4h"), seat.Hands[1].Hand.Cards.ToArray());

            round.Act(0, PlayerAction.Stand);

            // Croupier: 16 → pide 9s → 25, se pasa. Ganan las dos manos.
            Assert.Equal(RoundPhase.Complete, round.Phase);
            Assert.Equal(2, seat.Results.Count);
            Assert.All(seat.Results, r => Assert.Equal(HandOutcome.Win, r.Outcome));
            Assert.Equal(400m, seat.TotalReturned);
        }

        [Fact]
        public void AsesPartidos_RecibenUnaCartaYSeCierranSolos()
        {
            // s0: As Ah | croupier: 6d Tc (16) | Ks a la primera, 5h a la segunda, Kd al croupier
            Shoe shoe = StackedShoe("As", "6d", "Ah", "Tc", "Ks", "5h", "Kd");
            Round round = UnAsiento(shoe);
            round.Start();

            round.Act(0, PlayerAction.Split);

            // Sin turnos intermedios: ambas manos se resolvieron solas y la
            // ronda ya terminó.
            Assert.Equal(RoundPhase.Complete, round.Phase);

            Seat seat = round.Seats[0];
            Assert.Equal(2, seat.Hands.Count);
            Assert.Equal(21, seat.Hands[0].Hand.Value.Total);
            Assert.Equal(16, seat.Hands[1].Hand.Value.Total);
        }

        [Fact]
        public void VeintiunoTrasPartirAses_Paga1a1EnLaRondaCompleta()
        {
            // El mismo caso, pero comprobando el dinero de punta a punta.
            Shoe shoe = StackedShoe("As", "6d", "Ah", "Tc", "Ks", "5h", "Kd");
            Round round = UnAsiento(shoe, apuesta: 100m);
            round.Start();

            round.Act(0, PlayerAction.Split);

            Seat seat = round.Seats[0];

            Assert.False(seat.Hands[0].Hand.IsBlackjack);
            Assert.Equal(HandOutcome.Win, seat.Results[0].Outcome);
            Assert.Equal(200m, seat.Results[0].Returned); // 1:1, no 250

            // Croupier 16 + Kd = 26, se pasa: gana también la segunda mano.
            Assert.Equal(HandOutcome.Win, seat.Results[1].Outcome);
            Assert.Equal(400m, seat.TotalReturned);
        }

        [Fact]
        public void SinSaldoParaLaApuestaExtra_NoSePuedePartir()
        {
            Shoe shoe = StackedShoe("8s", "6d", "8h", "Tc", "3c", "4h", "9s");
            Round round = UnAsiento(shoe, apuesta: 100m, saldo: 50m);
            round.Start();

            Assert.DoesNotContain(PlayerAction.Split, round.CurrentLegalActions);
            Assert.Throws<InvalidOperationException>(() => round.Act(0, PlayerAction.Split));
        }

        // ------------------------------------------------------------------
        // Doblar
        // ------------------------------------------------------------------

        [Fact]
        public void Doblar_CobraLaApuestaExtraYCierraLaManoConUnaCarta()
        {
            // s0: 5s 6h (11) | croupier: 9d Tc (19) | Td al doblar
            Shoe shoe = StackedShoe("5s", "9d", "6h", "Tc", "Td");
            Round round = UnAsiento(shoe, apuesta: 100m, saldo: 1000m);
            round.Start();

            round.Act(0, PlayerAction.Double);

            Seat seat = round.Seats[0];
            Assert.Equal(900m, seat.AvailableBalance);
            Assert.Equal(200m, seat.Hands[0].Bet);
            Assert.Equal(3, seat.Hands[0].Hand.Count); // exactamente una carta más
            Assert.Equal(RoundPhase.Complete, round.Phase);

            // 21 contra 19: gana sobre la apuesta doblada.
            Assert.Equal(400m, seat.TotalReturned);
        }

        // ------------------------------------------------------------------
        // Seguro y peek
        // ------------------------------------------------------------------

        [Fact]
        public void ConAsDescubierto_SeOfreceSeguro()
        {
            Shoe shoe = StackedShoe("Ts", "Ad", "9h", "Kc");
            Round round = UnAsiento(shoe);

            round.Start();

            Assert.Equal(RoundPhase.Insurance, round.Phase);
            Assert.Contains(round.Events, e => e.Type == RoundEventType.InsuranceOffered);
        }

        [Fact]
        public void SeguroAcertado_CompensaLaManoPerdida()
        {
            // Croupier: Ad + Kc = blackjack.
            Shoe shoe = StackedShoe("Ts", "Ad", "9h", "Kc");
            Round round = UnAsiento(shoe, apuesta: 100m);
            round.Start();

            round.TakeInsurance(0, 50m);

            Assert.Equal(RoundPhase.Complete, round.Phase);
            Assert.Contains(round.Events, e => e.Type == RoundEventType.DealerBlackjack);

            Seat seat = round.Seats[0];
            Assert.Equal(HandOutcome.Lose, seat.Results[0].Outcome); // pierde 100
            Assert.Equal(150m, seat.InsuranceResult.Returned);       // recupera 150
            Assert.Equal(150m, seat.TotalReturned);
        }

        [Fact]
        public void SinBlackjackDelCroupier_ElSeguroSePierdeYSeSigueJugando()
        {
            // Ad + 9c: As descubierto pero sin figura debajo.
            Shoe shoe = StackedShoe("Ts", "Ad", "9h", "9c", "2s");
            Round round = UnAsiento(shoe, apuesta: 100m);
            round.Start();

            round.TakeInsurance(0, 50m);

            Assert.Equal(RoundPhase.PlayerTurns, round.Phase);
            Assert.Equal(0m, round.Seats[0].InsuranceResult.Returned);
            Assert.Equal(950m, round.Seats[0].AvailableBalance); // 1000 - 50 del seguro
        }

        [Fact]
        public void ElSeguroNoPuedeSuperarLaMitadDeLaApuesta()
        {
            Shoe shoe = StackedShoe("Ts", "Ad", "9h", "Kc");
            Round round = UnAsiento(shoe, apuesta: 100m);
            round.Start();

            Assert.Throws<ArgumentOutOfRangeException>(() => round.TakeInsurance(0, 51m));
        }

        [Fact]
        public void CerrarElSeguro_RechazaPorLosAusentes()
        {
            // Es lo que hace el servidor al expirar el temporizador.
            // Con dos asientos la descubierta es la TERCERA carta del zapato:
            // s0, s1, descubierta, s0, s1, tapada.
            Shoe shoe = StackedShoe("Ts", "8h", "Ad", "9h", "9c", "2s", "3s");
            var round = new Round(shoe, Vegas, new[]
            {
                new SeatBet(0, 100m, 1000m),
                new SeatBet(1, 100m, 1000m)
            });
            round.Start();

            round.TakeInsurance(0, 50m);
            Assert.Equal(RoundPhase.Insurance, round.Phase); // falta el asiento 1

            round.CloseInsurance();

            Assert.Equal(RoundPhase.PlayerTurns, round.Phase);
            Assert.True(round.Seats[1].InsuranceDecided);
            Assert.Equal(0m, round.Seats[1].InsuranceBet);
        }

        [Fact]
        public void ConFiguraDescubierta_HayPeekPeroNoSeguro()
        {
            // Kd descubierta + As tapado = blackjack. No se ofrece seguro
            // (solo se ofrece con As arriba) pero el peek acaba la ronda.
            Shoe shoe = StackedShoe("Ts", "Kd", "9h", "As");
            Round round = UnAsiento(shoe);

            round.Start();

            Assert.Equal(RoundPhase.Complete, round.Phase);
            Assert.DoesNotContain(round.Events, e => e.Type == RoundEventType.InsuranceOffered);
            Assert.Contains(round.Events, e => e.Type == RoundEventType.DealerBlackjack);
        }

        // ------------------------------------------------------------------
        // Croupier
        // ------------------------------------------------------------------

        [Fact]
        public void SiTodosSePasan_ElCroupierNoRoba()
        {
            // s0: Ts 9h (19) → pide 5s → 24. El croupier se queda en 16.
            Shoe shoe = StackedShoe("Ts", "6d", "9h", "Tc", "5s", "9c");
            Round round = UnAsiento(shoe);
            round.Start();

            round.Act(0, PlayerAction.Hit);

            Assert.Equal(RoundPhase.Complete, round.Phase);
            Assert.Equal(2, round.DealerHand.Count); // no robó el 9c
            Assert.Equal(16, round.DealerHand.Value.Total);
            Assert.Equal(HandOutcome.Bust, round.Seats[0].Results[0].Outcome);
        }

        [Fact]
        public void ConS17_ElCroupierSePlantaConDiecisieteBlando()
        {
            // Croupier: Ad + 6c = 17 blando. Con S17 no roba.
            Shoe shoe = StackedShoe("Ts", "Ad", "9h", "6c", "2s");
            Round round = UnAsiento(shoe);
            round.Start();

            round.DeclineInsurance(0);
            round.Act(0, PlayerAction.Stand);

            Assert.Equal(2, round.DealerHand.Count);
            Assert.Equal(17, round.DealerHand.Value.Total);
            Assert.Equal(HandOutcome.Win, round.Seats[0].Results[0].Outcome); // 19 gana a 17
        }

        [Fact]
        public void ConH17_ElCroupierPideConDiecisieteBlando()
        {
            Shoe shoe = StackedShoe("Ts", "Ad", "9h", "6c", "3s");
            var h17 = new TableRules(dealerHitsSoft17: true);
            Round round = UnAsiento(shoe, reglas: h17);
            round.Start();

            round.DeclineInsurance(0);
            round.Act(0, PlayerAction.Stand);

            Assert.Equal(3, round.DealerHand.Count); // robó el 3s
            Assert.Equal(20, round.DealerHand.Value.Total);
            Assert.Equal(HandOutcome.Lose, round.Seats[0].Results[0].Outcome);
        }

        // ------------------------------------------------------------------
        // Side bets dentro de la ronda
        // ------------------------------------------------------------------

        [Fact]
        public void LaSideBetSeCobra_AunqueLaManoPrincipalSePase()
        {
            // s0: 8h 8h (par perfecto) | croupier: 6d Tc | 9s hace bust
            Shoe shoe = StackedShoe("8h", "6d", "8h", "Tc", "9s", "5c");
            var round = new Round(shoe, Vegas, new[]
            {
                new SeatBet(0, 100m, 1000m, perfectPairsBet: 10m)
            });
            round.Start();

            Seat seat = round.Seats[0];
            Assert.True(seat.PerfectPairsResult.IsWin);
            Assert.Equal(260m, seat.PerfectPairsResult.Returned); // 10 + 250

            round.Act(0, PlayerAction.Hit); // 16 + 9 = 25

            Assert.Equal(HandOutcome.Bust, seat.Results[0].Outcome);
            Assert.Equal(260m, seat.TotalReturned); // solo la side bet
        }

        [Fact]
        public void El21Mas3_UsaLaCartaDescubiertaDelCroupier()
        {
            // s0: 5h 6h | croupier descubre 7h → escalera de color 40:1
            Shoe shoe = StackedShoe("5h", "7h", "6h", "Tc", "9s");
            var round = new Round(shoe, Vegas, new[]
            {
                new SeatBet(0, 100m, 1000m, twentyOnePlus3Bet: 5m)
            });

            round.Start();

            Seat seat = round.Seats[0];
            Assert.True(seat.TwentyOnePlus3Result.IsWin);
            Assert.Equal(40m, seat.TwentyOnePlus3Result.Multiplier);
            Assert.Equal(205m, seat.TwentyOnePlus3Result.Returned);
        }

        // ------------------------------------------------------------------
        // Validación de comandos
        // ------------------------------------------------------------------

        [Fact]
        public void JugarFueraDeTurno_SeRechaza()
        {
            Shoe shoe = StackedShoe("Ts", "6d", "8h", "9h", "Tc", "5s", "2c");
            var round = new Round(shoe, Vegas, new[]
            {
                new SeatBet(0, 100m, 1000m),
                new SeatBet(1, 100m, 1000m)
            });
            round.Start();

            Assert.Equal(0, round.CurrentSeatIndex);
            Assert.Throws<InvalidOperationException>(() => round.Act(1, PlayerAction.Hit));
        }

        [Fact]
        public void ActuarAntesDeRepartir_SeRechaza()
        {
            Shoe shoe = StackedShoe("Ts", "6d", "9h", "Tc");
            Round round = UnAsiento(shoe);

            Assert.Throws<InvalidOperationException>(() => round.Act(0, PlayerAction.Hit));
        }

        [Fact]
        public void ActuarConLaRondaTerminada_SeRechaza()
        {
            Shoe shoe = StackedShoe("Ts", "6d", "9h", "Tc", "5s");
            Round round = UnAsiento(shoe);
            round.Start();
            round.Act(0, PlayerAction.Stand);

            Assert.Equal(RoundPhase.Complete, round.Phase);
            Assert.Throws<InvalidOperationException>(() => round.Act(0, PlayerAction.Hit));
        }

        [Fact]
        public void ApuestaFueraDeLimites_SeRechazaAlCrearLaRonda()
        {
            Shoe shoe = StackedShoe("Ts", "6d", "9h", "Tc");

            Assert.Throws<ArgumentOutOfRangeException>(
                () => new Round(shoe, Vegas, new[] { new SeatBet(0, 99999m, 100000m) }));
        }

        [Fact]
        public void PlantarPorTiempo_AvanzaLaMesa()
        {
            Shoe shoe = StackedShoe("Ts", "6d", "9h", "Tc", "5s");
            Round round = UnAsiento(shoe);
            round.Start();

            round.ForceStand();

            Assert.Equal(RoundPhase.Complete, round.Phase);
        }

        // ------------------------------------------------------------------
        // Registro de eventos
        // ------------------------------------------------------------------

        [Fact]
        public void LaRondaTerminaConSuEventoDeCierre()
        {
            Shoe shoe = StackedShoe("Ts", "6d", "9h", "Tc", "5s");
            Round round = UnAsiento(shoe);
            round.Start();
            round.Act(0, PlayerAction.Stand);

            Assert.Equal(RoundEventType.RoundComplete, round.Events[round.Events.Count - 1].Type);
            Assert.Contains(round.Events, e => e.Type == RoundEventType.HandSettled);
        }

        [Fact]
        public void CadaCambioDeTurno_LlevaSusAccionesLegales()
        {
            Shoe shoe = StackedShoe("8s", "6d", "8h", "Tc", "3c", "4h", "9s");
            Round round = UnAsiento(shoe);
            round.Start();

            RoundEvent turno = round.Events.First(e => e.Type == RoundEventType.TurnChanged);

            Assert.NotNull(turno.LegalActions);
            Assert.Contains(PlayerAction.Split, turno.LegalActions!);
        }
    }
}
