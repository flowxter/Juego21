using Blackjack.Core.Hands;
using Blackjack.Core.Payouts;
using Blackjack.Core.Rules;
using Blackjack.Core.SideBets;
using Xunit;
using static Blackjack.Core.Tests.TestCards;

namespace Blackjack.Core.Tests
{
    public class PerfectPairsTests
    {
        private static readonly PerfectPairs Mesa = PerfectPairs.Standard;

        [Fact]
        public void ParPerfecto_MismaFiguraYPalo_Paga25a1()
        {
            SideBetResolution r = Mesa.Resolve(C("7h"), C("7h"), 10m);

            Assert.Equal(PerfectPairsCategory.PerfectPair, PerfectPairs.Categorize(C("7h"), C("7h")));
            Assert.Equal(25m, r.Multiplier);
            Assert.Equal(260m, r.Returned); // 10 + 250
        }

        [Fact]
        public void ParDelMismoColor_Paga12a1()
        {
            // Corazones y diamantes: mismo color, distinto palo.
            SideBetResolution r = Mesa.Resolve(C("7h"), C("7d"), 10m);

            Assert.Equal(12m, r.Multiplier);
            Assert.Equal(130m, r.Returned);
        }

        [Fact]
        public void ParMixto_Paga6a1()
        {
            SideBetResolution r = Mesa.Resolve(C("7h"), C("7c"), 10m);

            Assert.Equal(6m, r.Multiplier);
            Assert.Equal(70m, r.Returned);
        }

        [Fact]
        public void FigurasDistintasDelMismoValor_NoSonPar()
        {
            // K-Q se pueden PARTIR en la mano principal (ambas valen 10) pero
            // no son par para Perfect Pairs, que exige la misma figura.
            SideBetResolution r = Mesa.Resolve(C("Kh"), C("Qh"), 10m);

            Assert.False(r.IsWin);
            Assert.Equal(0m, r.Returned);
            Assert.Equal(-10m, r.NetProfit);
        }

        [Fact]
        public void SinPar_SePierde()
        {
            Assert.False(Mesa.Resolve(C("7h"), C("9s"), 10m).IsWin);
        }
    }

    public class TwentyOnePlus3Tests
    {
        private static readonly TwentyOnePlus3 Mesa = TwentyOnePlus3.Standard;

        [Fact]
        public void TrioDelMismoPalo_Paga100a1()
        {
            // Solo posible con varias barajas en el zapato.
            SideBetResolution r = Mesa.Resolve(C("7h"), C("7h"), C("7h"), 5m);

            Assert.Equal(100m, r.Multiplier);
            Assert.Equal(505m, r.Returned);
        }

        [Fact]
        public void EscaleraDeColor_Paga40a1()
        {
            SideBetResolution r = Mesa.Resolve(C("5h"), C("6h"), C("7h"), 5m);

            Assert.Equal(TwentyOnePlus3Category.StraightFlush, TwentyOnePlus3.Categorize(C("5h"), C("6h"), C("7h")));
            Assert.Equal(40m, r.Multiplier);
        }

        [Fact]
        public void Trio_Paga30a1()
        {
            SideBetResolution r = Mesa.Resolve(C("7h"), C("7s"), C("7c"), 5m);

            Assert.Equal(30m, r.Multiplier);
        }

        [Fact]
        public void Escalera_Paga10a1()
        {
            SideBetResolution r = Mesa.Resolve(C("5h"), C("6s"), C("7c"), 5m);

            Assert.Equal(10m, r.Multiplier);
        }

        [Fact]
        public void Color_Paga5a1()
        {
            SideBetResolution r = Mesa.Resolve(C("2h"), C("7h"), C("Kh"), 5m);

            Assert.Equal(5m, r.Multiplier);
        }

        [Fact]
        public void ElAsCuentaBajo_EnLaEscaleraA23()
        {
            Assert.Equal(
                TwentyOnePlus3Category.Straight,
                TwentyOnePlus3.Categorize(C("Ah"), C("2s"), C("3c")));
        }

        [Fact]
        public void ElAsCuentaAlto_EnLaEscaleraQKA()
        {
            Assert.Equal(
                TwentyOnePlus3Category.Straight,
                TwentyOnePlus3.Categorize(C("Qh"), C("Ks"), C("Ac")));
        }

        [Fact]
        public void LaEscaleraNoEsCircular_KA2NoVale()
        {
            Assert.Equal(
                TwentyOnePlus3Category.None,
                TwentyOnePlus3.Categorize(C("Kh"), C("As"), C("2c")));
        }

        [Fact]
        public void ElOrdenDeLasCartas_NoImporta()
        {
            Assert.Equal(
                TwentyOnePlus3Category.Straight,
                TwentyOnePlus3.Categorize(C("7c"), C("5h"), C("6s")));
        }

        [Fact]
        public void ManoSinCombinacion_SePierde()
        {
            Assert.False(Mesa.Resolve(C("2h"), C("7s"), C("Kc"), 5m).IsWin);
        }
    }

    public class SideBetIndependenceTests
    {
        [Fact]
        public void LaSideBetSeCobra_AunqueLaManoPrincipalSePase()
        {
            // Caso fácil de romper por acoplamiento: el jugador liga par
            // perfecto de ochos, parte, y acaba pasándose. El par se paga igual.
            SideBetResolution side = PerfectPairs.Standard.Resolve(C("8h"), C("8h"), 10m);

            PlayerHand principal = MakePlayerHand(100m, "8h", "8h", "9c"); // 25
            Hand croupier = MakeHand("Td", "9c");
            HandResolution main = PayoutCalculator.Resolve(principal, croupier, TableRules.VegasStrip);

            Assert.True(side.IsWin);
            Assert.Equal(250m, side.NetProfit);

            Assert.Equal(HandOutcome.Bust, main.Outcome);
            Assert.Equal(-100m, main.NetProfit);

            // Neto de la ronda: +150 pese a perder la mano principal.
            Assert.Equal(150m, side.NetProfit + main.NetProfit);
        }
    }
}
