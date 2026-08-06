using System;
using System.Collections.Generic;
using Blackjack.Core.Cards;
using Blackjack.Core.Hands;
using Blackjack.Core.Shuffling;

namespace Blackjack.Core.Tests
{
    /// <summary>
    /// Azúcar sintáctico para los tests. Notación de póker estándar:
    /// figura (2-9, T, J, Q, K, A) + palo (c, d, h, s).
    /// "As" = as de picas, "Th" = diez de corazones.
    /// </summary>
    internal static class TestCards
    {
        public static Card C(string notation)
        {
            if (notation == null || notation.Length != 2)
                throw new ArgumentException("Usa notación de 2 caracteres, p. ej. \"As\" o \"Th\".", nameof(notation));

            Rank rank = char.ToUpperInvariant(notation[0]) switch
            {
                '2' => Rank.Two,
                '3' => Rank.Three,
                '4' => Rank.Four,
                '5' => Rank.Five,
                '6' => Rank.Six,
                '7' => Rank.Seven,
                '8' => Rank.Eight,
                '9' => Rank.Nine,
                'T' => Rank.Ten,
                'J' => Rank.Jack,
                'Q' => Rank.Queen,
                'K' => Rank.King,
                'A' => Rank.Ace,
                _ => throw new ArgumentException("Figura desconocida: " + notation[0], nameof(notation))
            };

            Suit suit = char.ToLowerInvariant(notation[1]) switch
            {
                'c' => Suit.Clubs,
                'd' => Suit.Diamonds,
                'h' => Suit.Hearts,
                's' => Suit.Spades,
                _ => throw new ArgumentException("Palo desconocido: " + notation[1], nameof(notation))
            };

            return new Card(rank, suit);
        }

        public static Card[] Many(params string[] notations)
        {
            var cards = new Card[notations.Length];
            for (int i = 0; i < notations.Length; i++) cards[i] = C(notations[i]);
            return cards;
        }

        /// <summary>
        /// Zapato con el orden exacto que se le pase. Sin esto habría que
        /// buscar semillas que por casualidad produzcan la situación a probar.
        ///
        /// Orden de reparto con N asientos: una carta a cada asiento, luego la
        /// descubierta del croupier, luego la segunda a cada asiento y por
        /// último la tapada.
        /// </summary>
        public static Shoe StackedShoe(params string[] notations)
            => Shoe.CreateStacked(Many(notations));

        public static Hand MakeHand(params string[] notations)
            => new Hand(Many(notations));

        public static PlayerHand MakePlayerHand(decimal bet, params string[] notations)
        {
            var hand = new PlayerHand(bet);
            foreach (string n in notations) hand.Deal(C(n));
            return hand;
        }

        /// <summary>
        /// Mano nacida de un split. <paramref name="splitAces"/> marca el caso
        /// especial de los ases partidos.
        /// </summary>
        public static PlayerHand MakeSplitHand(decimal bet, bool splitAces, params string[] notations)
        {
            var hand = new PlayerHand(bet, splitDepth: 1, isFromSplit: true, isSplitAces: splitAces);
            foreach (string n in notations) hand.Deal(C(n));
            return hand;
        }
    }
}
