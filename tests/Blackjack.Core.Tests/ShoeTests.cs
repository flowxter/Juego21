using System;
using System.Collections.Generic;
using Blackjack.Core.Cards;
using Blackjack.Core.Shuffling;
using Xunit;

namespace Blackjack.Core.Tests
{
    public class ShoeTests
    {
        [Theory]
        [InlineData(1, 52)]
        [InlineData(6, 312)]
        [InlineData(8, 416)]
        public void ElZapato_TieneCincuentaYDosCartasPorBaraja(int barajas, int esperado)
        {
            var shoe = new Shoe(barajas, random: new SeededRandomSource(1));
            Assert.Equal(esperado, shoe.TotalCards);
        }

        [Fact]
        public void CadaCarta_ApareceUnaVezPorBaraja()
        {
            const int barajas = 6;
            var shoe = new Shoe(barajas, penetration: 0.85, random: new SeededRandomSource(42));

            var counts = new Dictionary<byte, int>();
            int total = shoe.TotalCards;

            // Agotamos el zapato entero contando qué sale.
            for (int i = 0; i < total; i++)
            {
                Card card = shoe.Draw();
                counts.TryGetValue(card.Id, out int n);
                counts[card.Id] = n + 1;
            }

            Assert.Equal(52, counts.Count);
            foreach (KeyValuePair<byte, int> entry in counts)
            {
                Assert.Equal(barajas, entry.Value);
            }
        }

        [Fact]
        public void BarajarNoPierdeNiDuplicaCartas()
        {
            var shoe = new Shoe(2, random: new SeededRandomSource(7));

            shoe.Draw();
            shoe.Draw();
            shoe.Shuffle();

            Assert.Equal(0, shoe.CardsDealt);
            Assert.Equal(shoe.TotalCards, shoe.CardsRemaining);
            Assert.False(shoe.NeedsShuffle);
        }

        [Fact]
        public void LaCutCard_SaltaAlSuperarLaPenetracion()
        {
            var shoe = new Shoe(6, penetration: 0.75, random: new SeededRandomSource(3));
            int cutAt = (int)(shoe.TotalCards * 0.75); // 234 de 312

            for (int i = 0; i < cutAt - 1; i++) shoe.Draw();
            Assert.False(shoe.NeedsShuffle);

            shoe.Draw();
            Assert.True(shoe.NeedsShuffle);
        }

        [Fact]
        public void TrasLaCutCard_SePuedeSeguirRepartiendo()
        {
            // Comportamiento de casino: al cruzar la cut card la ronda EN CURSO
            // se termina con normalidad. Barajar a media ronda sería un bug.
            var shoe = new Shoe(6, penetration: 0.75, random: new SeededRandomSource(9));

            while (!shoe.NeedsShuffle) shoe.Draw();

            Exception? error = Record.Exception(() =>
            {
                for (int i = 0; i < 20; i++) shoe.Draw();
            });

            Assert.Null(error);
        }

        [Fact]
        public void ZapatoAgotado_Lanza()
        {
            var shoe = new Shoe(1, penetration: 0.85, random: new SeededRandomSource(11));

            for (int i = 0; i < 52; i++) shoe.Draw();

            Assert.Throws<InvalidOperationException>(() => shoe.Draw());
        }

        [Fact]
        public void MismaSemilla_ProduceElMismoOrden()
        {
            var a = new Shoe(6, random: new SeededRandomSource(12345));
            var b = new Shoe(6, random: new SeededRandomSource(12345));

            for (int i = 0; i < 100; i++)
            {
                Assert.Equal(a.Draw(), b.Draw());
            }
        }

        [Fact]
        public void SemillasDistintas_ProducenOrdenesDistintos()
        {
            var a = new Shoe(6, random: new SeededRandomSource(1));
            var b = new Shoe(6, random: new SeededRandomSource(2));

            bool algunaDiferencia = false;
            for (int i = 0; i < 100; i++)
            {
                if (a.Draw() != b.Draw()) { algunaDiferencia = true; break; }
            }

            Assert.True(algunaDiferencia, "Dos semillas distintas dieron el mismo reparto.");
        }

        [Fact]
        public void PenetracionExcesiva_SeRechaza()
        {
            // Por encima de 0.85 el zapato puede quedarse sin cartas con una
            // mesa llena de splits. Preferimos fallar al crear la mesa.
            Assert.Throws<ArgumentOutOfRangeException>(() => new Shoe(6, penetration: 0.95));
        }

        [Fact]
        public void ElBarajado_DistribuyeLasCartasPorTodoElZapato()
        {
            // Comprobación de cordura del Fisher-Yates: tras muchos barajados,
            // una carta concreta debe haber aparecido en zonas muy distintas.
            // Un barajado roto tiende a dejarla cerca de su posición inicial.
            var random = new SeededRandomSource(2024);
            var posiciones = new List<int>();

            for (int iteracion = 0; iteracion < 200; iteracion++)
            {
                var shoe = new Shoe(1, penetration: 0.85, random: random);
                var objetivo = new Card(Rank.Ace, Suit.Spades);

                for (int i = 0; i < 52; i++)
                {
                    if (shoe.Draw() == objetivo) { posiciones.Add(i); break; }
                }
            }

            Assert.Equal(200, posiciones.Count);

            double media = 0;
            foreach (int p in posiciones) media += p;
            media /= posiciones.Count;

            // La media esperada es 25.5. Damos margen amplio para no crear un
            // test escamoso: solo queremos detectar un sesgo grosero.
            Assert.InRange(media, 20.0, 31.0);
        }
    }
}
