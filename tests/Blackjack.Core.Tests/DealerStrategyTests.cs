using Blackjack.Core.Rules;
using Xunit;
using static Blackjack.Core.Tests.TestCards;

namespace Blackjack.Core.Tests
{
    public class DealerStrategyTests
    {
        private static readonly TableRules S17 = TableRules.VegasStrip;
        private static readonly TableRules H17 = new TableRules(dealerHitsSoft17: true);

        [Theory]
        [InlineData("2s", "3h")]   // 5
        [InlineData("Ts", "6h")]   // 16
        [InlineData("9s", "7h")]   // 16
        public void ConMenosDe17_ElCroupierPide(params string[] cartas)
        {
            Assert.True(DealerStrategy.ShouldHit(MakeHand(cartas), S17));
        }

        [Theory]
        [InlineData("Ts", "7h")]   // 17 duro
        [InlineData("Ts", "8h")]   // 18
        [InlineData("Ts", "Kh")]   // 20
        public void Con17OMas_ElCroupierSePlanta(params string[] cartas)
        {
            Assert.False(DealerStrategy.ShouldHit(MakeHand(cartas), S17));
        }

        [Fact]
        public void ConS17_SePlantaConDiecisieteBlando()
        {
            // A-6. Es la regla que anuncian las dos mesas de referencia.
            Assert.False(DealerStrategy.ShouldHit(MakeHand("Ah", "6c"), S17));
        }

        [Fact]
        public void ConH17_PideConDiecisieteBlando()
        {
            Assert.True(DealerStrategy.ShouldHit(MakeHand("Ah", "6c"), H17));
        }

        [Fact]
        public void ConH17_SigueplantandoseConDiecisieteDuro()
        {
            // La diferencia entre S17 y H17 afecta SOLO al 17 blando.
            Assert.False(DealerStrategy.ShouldHit(MakeHand("Ts", "7h"), H17));
        }

        [Fact]
        public void ConDieciochoBlando_SePlantaEnAmbasVariantes()
        {
            Assert.False(DealerStrategy.ShouldHit(MakeHand("Ah", "7c"), S17));
            Assert.False(DealerStrategy.ShouldHit(MakeHand("Ah", "7c"), H17));
        }
    }
}
