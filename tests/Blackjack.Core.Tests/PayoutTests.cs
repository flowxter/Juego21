using Blackjack.Core.Hands;
using Blackjack.Core.Payouts;
using Blackjack.Core.Rules;
using Xunit;
using static Blackjack.Core.Tests.TestCards;

namespace Blackjack.Core.Tests
{
    public class PayoutTests
    {
        private static readonly TableRules Vegas = TableRules.VegasStrip;

        [Fact]
        public void Blackjack_Paga3a2()
        {
            PlayerHand jugador = MakePlayerHand(100m, "As", "Kh");
            Hand croupier = MakeHand("9d", "8c"); // 17

            HandResolution r = PayoutCalculator.Resolve(jugador, croupier, Vegas);

            Assert.Equal(HandOutcome.PlayerBlackjack, r.Outcome);
            Assert.Equal(250m, r.Returned);  // 100 apuesta + 150 premio
            Assert.Equal(150m, r.NetProfit);
        }

        [Fact]
        public void BlackjackContraBlackjack_EsEmpate()
        {
            PlayerHand jugador = MakePlayerHand(100m, "As", "Kh");
            Hand croupier = MakeHand("Ad", "Qc");

            HandResolution r = PayoutCalculator.Resolve(jugador, croupier, Vegas);

            Assert.Equal(HandOutcome.Push, r.Outcome);
            Assert.Equal(100m, r.Returned);
            Assert.Equal(0m, r.NetProfit);
        }

        [Fact]
        public void PasarseSiemprePierde_AunqueElCroupierTambienSePase()
        {
            // Esta regla es de donde sale casi toda la ventaja de la casa.
            // Si alguna vez "se arregla", la mesa se vuelve favorable al jugador.
            PlayerHand jugador = MakePlayerHand(100m, "Ts", "9h", "5c"); // 24
            Hand croupier = MakeHand("Td", "8s", "7c");                  // 25

            HandResolution r = PayoutCalculator.Resolve(jugador, croupier, Vegas);

            Assert.Equal(HandOutcome.Bust, r.Outcome);
            Assert.Equal(0m, r.Returned);
            Assert.Equal(-100m, r.NetProfit);
        }

        [Fact]
        public void CroupierPasado_HaceGanarAQuienSigueEnPie()
        {
            PlayerHand jugador = MakePlayerHand(100m, "Ts", "6h"); // 16
            Hand croupier = MakeHand("Td", "8s", "7c");            // 25

            HandResolution r = PayoutCalculator.Resolve(jugador, croupier, Vegas);

            Assert.Equal(HandOutcome.Win, r.Outcome);
            Assert.Equal(200m, r.Returned);
        }

        [Theory]
        [InlineData("Ts", "Kh", HandOutcome.Win)]   // 20 contra 19
        [InlineData("Ts", "9h", HandOutcome.Push)]  // 19 contra 19
        [InlineData("Ts", "8h", HandOutcome.Lose)]  // 18 contra 19
        public void PorPuntos_GanaElMasAlto(string primera, string segunda, HandOutcome esperado)
        {
            PlayerHand jugador = MakePlayerHand(50m, primera, segunda);
            Hand croupier = MakeHand("Td", "9c"); // 19

            HandResolution r = PayoutCalculator.Resolve(jugador, croupier, Vegas);

            Assert.Equal(esperado, r.Outcome);
        }

        [Fact]
        public void Rendirse_DevuelveLaMitad()
        {
            PlayerHand jugador = MakePlayerHand(100m, "Ts", "6h");
            jugador.Surrender();
            Hand croupier = MakeHand("Ad", "Qc"); // incluso con blackjack

            HandResolution r = PayoutCalculator.Resolve(jugador, croupier, Vegas);

            Assert.Equal(HandOutcome.Surrender, r.Outcome);
            Assert.Equal(50m, r.Returned);
            Assert.Equal(-50m, r.NetProfit);
        }

        [Fact]
        public void ManoDoblada_CobraSobreLaApuestaDuplicada()
        {
            var jugador = new PlayerHand(100m);
            jugador.Deal(C("5s"));
            jugador.Deal(C("6h"));   // 11
            jugador.Double();        // apuesta pasa a 200
            jugador.Deal(C("Td"));   // 21, se cierra sola

            Assert.Equal(200m, jugador.Bet);
            Assert.True(jugador.IsFinished);

            Hand croupier = MakeHand("Tc", "9s"); // 19
            HandResolution r = PayoutCalculator.Resolve(jugador, croupier, Vegas);

            Assert.Equal(HandOutcome.Win, r.Outcome);
            Assert.Equal(400m, r.Returned);
            Assert.Equal(200m, r.NetProfit);
        }

        [Fact]
        public void VeintiunoTrasPartir_Paga1a1_No3a2()
        {
            // Si este test falla, cada split con 21 regala media apuesta.
            PlayerHand jugador = MakeSplitHand(100m, splitAces: true, "As", "Kh");
            Hand croupier = MakeHand("Td", "9c"); // 19

            HandResolution r = PayoutCalculator.Resolve(jugador, croupier, Vegas);

            Assert.Equal(HandOutcome.Win, r.Outcome);
            Assert.Equal(200m, r.Returned); // no 250
        }

        [Fact]
        public void SeguroPaga2a1_SiElCroupierTieneBlackjack()
        {
            Hand croupier = MakeHand("Ad", "Kc");

            HandResolution r = PayoutCalculator.ResolveInsurance(50m, croupier, Vegas);

            Assert.Equal(HandOutcome.Win, r.Outcome);
            Assert.Equal(150m, r.Returned); // 50 apuesta + 100 premio
        }

        [Fact]
        public void SeguroSePierde_SiElCroupierNoTieneBlackjack()
        {
            Hand croupier = MakeHand("Ad", "9c"); // As pero sin figura

            HandResolution r = PayoutCalculator.ResolveInsurance(50m, croupier, Vegas);

            Assert.Equal(HandOutcome.Lose, r.Outcome);
            Assert.Equal(0m, r.Returned);
        }

        [Fact]
        public void ElSeguroMaximo_EsLaMitadDeLaApuesta()
        {
            Assert.Equal(50m, PayoutCalculator.MaxInsuranceBet(100m));
        }

        [Fact]
        public void SeguroYManoPrincipal_SeLiquidanPorSeparado()
        {
            // Caso real: pagas seguro, el croupier tiene blackjack. Pierdes la
            // mano principal pero el seguro te deja a cero. Es justo el motivo
            // por el que existe, aunque a la larga siga siendo mala apuesta.
            PlayerHand jugador = MakePlayerHand(100m, "Ts", "9h");
            Hand croupier = MakeHand("Ad", "Kc");

            HandResolution principal = PayoutCalculator.Resolve(jugador, croupier, Vegas);
            HandResolution seguro = PayoutCalculator.ResolveInsurance(50m, croupier, Vegas);

            Assert.Equal(-100m, principal.NetProfit);
            Assert.Equal(100m, seguro.NetProfit);
            Assert.Equal(0m, principal.NetProfit + seguro.NetProfit);
        }
    }
}
