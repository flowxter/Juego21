using System;
using System.Collections.Generic;
using System.Text;
using Blackjack.Core.Cards;

namespace Blackjack.Data
{
    /// <summary>
    /// Convierte cartas a texto para guardarlas en el historial.
    ///
    /// Se usa notación de póker ("As,Kh") en vez de los ids numéricos porque
    /// el historial se lee: al depurar una reclamación, "As,Kh" se entiende
    /// de un vistazo y "38,24" no.
    /// </summary>
    public static class CardCodec
    {
        public static string Encode(IReadOnlyList<Card> cards)
        {
            if (cards == null || cards.Count == 0) return string.Empty;

            var sb = new StringBuilder(cards.Count * 3);

            for (int i = 0; i < cards.Count; i++)
            {
                if (i > 0) sb.Append(',');
                sb.Append(RankLetter(cards[i].Rank));
                sb.Append(SuitLetter(cards[i].Suit));
            }

            return sb.ToString();
        }

        public static IReadOnlyList<Card> Decode(string encoded)
        {
            if (string.IsNullOrWhiteSpace(encoded)) return Array.Empty<Card>();

            string[] parts = encoded.Split(',');
            var cards = new List<Card>(parts.Length);

            foreach (string part in parts)
            {
                string token = part.Trim();
                if (token.Length != 2) continue;
                cards.Add(new Card(ParseRank(token[0]), ParseSuit(token[1])));
            }

            return cards;
        }

        private static char RankLetter(Rank rank) => rank switch
        {
            Rank.Ace => 'A',
            Rank.King => 'K',
            Rank.Queen => 'Q',
            Rank.Jack => 'J',
            Rank.Ten => 'T',
            _ => (char)('0' + (int)rank)
        };

        private static char SuitLetter(Suit suit) => suit switch
        {
            Suit.Clubs => 'c',
            Suit.Diamonds => 'd',
            Suit.Hearts => 'h',
            _ => 's'
        };

        private static Rank ParseRank(char c) => char.ToUpperInvariant(c) switch
        {
            'A' => Rank.Ace,
            'K' => Rank.King,
            'Q' => Rank.Queen,
            'J' => Rank.Jack,
            'T' => Rank.Ten,
            _ => (Rank)(c - '0')
        };

        private static Suit ParseSuit(char c) => char.ToLowerInvariant(c) switch
        {
            'c' => Suit.Clubs,
            'd' => Suit.Diamonds,
            'h' => Suit.Hearts,
            _ => Suit.Spades
        };
    }
}
