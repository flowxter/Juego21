using System;
using System.Collections.Generic;
using Blackjack.Core.Cards;

namespace Blackjack.Core.Shuffling
{
    /// <summary>
    /// El zapato: N barajas mezcladas con una cut card que marca cuándo hay
    /// que rebarajar.
    ///
    /// Comportamiento de casino real: al cruzar la cut card la ronda en curso
    /// SE TERMINA con normalidad y se baraja antes de la siguiente. Nunca se
    /// baraja a media ronda. Por eso <see cref="NeedsShuffle"/> es solo una
    /// señal que la mesa consulta entre rondas, no algo que el shoe imponga.
    /// </summary>
    public sealed class Shoe
    {
        public const int DefaultDeckCount = 6;
        public const double DefaultPenetration = 0.75;

        private readonly Card[] _cards;
        private readonly IRandomSource _random;
        private int _position;
        private int _cutCardIndex;

        public Shoe(int deckCount = DefaultDeckCount, double penetration = DefaultPenetration, IRandomSource? random = null)
        {
            if (deckCount < 1 || deckCount > 8)
                throw new ArgumentOutOfRangeException(nameof(deckCount), deckCount, "El zapato admite entre 1 y 8 barajas.");
            if (penetration <= 0.0 || penetration > 0.85)
                throw new ArgumentOutOfRangeException(nameof(penetration), penetration,
                    "La penetración debe estar en (0, 0.85]. Por encima de 0.85 el zapato puede agotarse a media ronda.");

            DeckCount = deckCount;
            Penetration = penetration;
            _random = random ?? new CryptoRandomSource();

            _cards = BuildOrderedShoe(deckCount);
            Shuffle();
        }

        /// <summary>
        /// Zapato con el orden fijado de antemano, sin barajar. Solo para
        /// tests: permite montar la situación exacta que se quiere probar
        /// (ases partidos, blackjack del croupier, un 21+3 concreto) en vez
        /// de buscar semillas que casualmente la produzcan.
        /// </summary>
        private Shoe(Card[] stacked)
        {
            DeckCount = 1;
            Penetration = DefaultPenetration;
            _random = new SeededRandomSource(0);
            _cards = stacked;
            _position = 0;
            _cutCardIndex = stacked.Length; // nunca pide barajar a media prueba
            ShuffleCount = 1;
        }

        internal static Shoe CreateStacked(params Card[] cards)
        {
            if (cards == null || cards.Length == 0)
                throw new ArgumentException("Hace falta al menos una carta.", nameof(cards));

            return new Shoe(cards);
        }

        public int DeckCount { get; }

        public double Penetration { get; }

        /// <summary>Total de cartas del zapato lleno (52 × barajas).</summary>
        public int TotalCards => _cards.Length;

        /// <summary>Cartas ya repartidas desde el último barajado.</summary>
        public int CardsDealt => _position;

        /// <summary>Cartas físicamente disponibles antes de agotar el zapato.</summary>
        public int CardsRemaining => _cards.Length - _position;

        /// <summary>
        /// True cuando se ha cruzado la cut card. La mesa lo consulta al
        /// terminar la ronda para decidir si baraja.
        /// </summary>
        public bool NeedsShuffle => _position >= _cutCardIndex;

        /// <summary>
        /// Nº de barajado desde que se creó el zapato. Lo usa el registro de
        /// manos para agrupar rondas del mismo shoe.
        /// </summary>
        public int ShuffleCount { get; private set; }

        private static Card[] BuildOrderedShoe(int deckCount)
        {
            var cards = new Card[52 * deckCount];
            int i = 0;

            for (int d = 0; d < deckCount; d++)
            {
                for (int rank = (int)Rank.Two; rank <= (int)Rank.Ace; rank++)
                {
                    for (int suit = 0; suit <= 3; suit++)
                    {
                        cards[i++] = new Card((Rank)rank, (Suit)suit);
                    }
                }
            }

            return cards;
        }

        /// <summary>
        /// Baraja con Fisher-Yates y recoloca la cut card. Fisher-Yates
        /// recorrido hacia atrás es el único barajado que produce las n!
        /// permutaciones con igual probabilidad; las variantes "intuitivas"
        /// (intercambiar con un índice aleatorio cualquiera) están sesgadas.
        /// </summary>
        public void Shuffle()
        {
            for (int i = _cards.Length - 1; i > 0; i--)
            {
                int j = _random.NextInt(i + 1);
                if (i == j) continue;

                Card tmp = _cards[i];
                _cards[i] = _cards[j];
                _cards[j] = tmp;
            }

            _position = 0;
            _cutCardIndex = (int)(_cards.Length * Penetration);
            ShuffleCount++;
        }

        /// <summary>Saca la siguiente carta del zapato.</summary>
        public Card Draw()
        {
            if (_position >= _cards.Length)
                throw new InvalidOperationException(
                    "El zapato se ha agotado a media ronda. Con penetración ≤ 0.85 esto no debería ocurrir: revisa si algo está robando cartas fuera de la ronda.");

            return _cards[_position++];
        }

        /// <summary>
        /// Reparte varias cartas de golpe. Útil para el reparto inicial.
        /// </summary>
        public IReadOnlyList<Card> Draw(int count)
        {
            if (count < 0) throw new ArgumentOutOfRangeException(nameof(count));

            var drawn = new Card[count];
            for (int i = 0; i < count; i++) drawn[i] = Draw();
            return drawn;
        }
    }
}
