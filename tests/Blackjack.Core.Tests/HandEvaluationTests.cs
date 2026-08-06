using Blackjack.Core.Cards;
using Blackjack.Core.Hands;
using Xunit;
using static Blackjack.Core.Tests.TestCards;

namespace Blackjack.Core.Tests
{
    public class HandEvaluationTests
    {
        [Theory]
        [InlineData(20, "Ts", "Kh")]
        [InlineData(17, "Ts", "7h")]
        [InlineData(6, "2s", "4h")]
        [InlineData(30, "Ts", "Kh", "Qc")]
        public void ManoSinAses_SumaDirecta(int esperado, params string[] cartas)
        {
            Assert.Equal(esperado, MakeHand(cartas).Value.Total);
        }

        [Fact]
        public void UnAs_SePromueveA11_SiCabe()
        {
            Hand hand = MakeHand("Ah", "6c"); // 17 blando

            Assert.Equal(17, hand.Value.Total);
            Assert.True(hand.Value.IsSoft);
            Assert.Equal(7, hand.Value.HardTotal);
        }

        [Fact]
        public void ElAs_BajaA1_CuandoLa11Pasaria()
        {
            Hand hand = MakeHand("Ah", "6c", "Td"); // 17 duro, no 27

            Assert.Equal(17, hand.Value.Total);
            Assert.False(hand.Value.IsSoft);
        }

        [Fact]
        public void DosAses_SoloUnoValePor11()
        {
            // Nunca pueden valer 11 los dos a la vez: serían 22.
            Hand hand = MakeHand("Ah", "As");

            Assert.Equal(12, hand.Value.Total);
            Assert.True(hand.Value.IsSoft);
        }

        [Fact]
        public void CuatroAses_Suman14Blando()
        {
            Hand hand = MakeHand("Ah", "As", "Ac", "Ad");

            Assert.Equal(14, hand.Value.Total);
            Assert.True(hand.Value.IsSoft);
        }

        [Fact]
        public void ManoBlanda_SeVuelveDura_AlPasarseDe21()
        {
            Hand hand = MakeHand("Ah", "9c"); // 20 blando
            Assert.True(hand.Value.IsSoft);

            hand.Add(C("5d")); // 15 duro
            Assert.Equal(15, hand.Value.Total);
            Assert.False(hand.Value.IsSoft);
            Assert.False(hand.Value.IsBust);
        }

        [Fact]
        public void Blackjack_EsAsMasDiez_EnDosCartas()
        {
            Assert.True(MakeHand("Ah", "Ks").IsBlackjack);
            Assert.True(MakeHand("Td", "Ac").IsBlackjack);
        }

        [Fact]
        public void Veintiuno_ConTresCartas_NoEsBlackjack()
        {
            Hand hand = MakeHand("7h", "7s", "7c");

            Assert.Equal(21, hand.Value.Total);
            Assert.False(hand.IsBlackjack);
        }

        [Fact]
        public void Veintiuno_EnManoPartida_NoEsBlackjack()
        {
            // Este es el caso que más se escapa: 21 tras partir paga 1:1,
            // no 3:2. Si esto falla, la mesa regala dinero en cada split.
            var hand = new Hand(Many("As", "Kh"), isFromSplit: true, isSplitAces: true);

            Assert.Equal(21, hand.Value.Total);
            Assert.False(hand.IsBlackjack);
        }

        [Fact]
        public void Pasarse_SeDetectaPorEncimaDe21()
        {
            Hand hand = MakeHand("Ts", "9h", "5c");

            Assert.Equal(24, hand.Value.Total);
            Assert.True(hand.Value.IsBust);
        }

        [Fact]
        public void Par_SeDetectaPorValor_NoPorFigura()
        {
            // K-Q son partibles porque ambas valen 10, aunque no sean la
            // misma figura. Perfect Pairs, en cambio, exige figura idéntica.
            Hand kq = MakeHand("Kh", "Qs");

            Assert.True(kq.IsPair);
            Assert.False(kq.IsExactRankPair);
        }

        [Theory]
        [InlineData(0)]
        [InlineData(13)]
        [InlineData(51)]
        public void Card_IdaYVueltaPorId_ConservaLaCarta(byte id)
        {
            Card card = Card.FromId(id);
            Assert.Equal(id, card.Id);
        }

        [Fact]
        public void Card_TodosLosIds_SonUnicos()
        {
            var seen = new System.Collections.Generic.HashSet<byte>();

            for (int rank = 2; rank <= 14; rank++)
            {
                for (int suit = 0; suit <= 3; suit++)
                {
                    var card = new Card((Rank)rank, (Suit)suit);
                    Assert.True(seen.Add(card.Id), "Id duplicado en " + card);
                }
            }

            Assert.Equal(52, seen.Count);
        }
    }
}
