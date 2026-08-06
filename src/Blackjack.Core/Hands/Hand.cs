using System;
using System.Collections.Generic;
using System.Text;
using Blackjack.Core.Cards;

namespace Blackjack.Core.Hands
{
    /// <summary>
    /// Conjunto de cartas y su valoración. No sabe nada de apuestas ni de
    /// turnos: eso vive en la capa de ronda. Aquí solo se cuentan puntos.
    /// </summary>
    public sealed class Hand
    {
        private readonly List<Card> _cards;
        private HandValue _value;

        public Hand(bool isFromSplit = false, bool isSplitAces = false)
        {
            _cards = new List<Card>(6);
            IsFromSplit = isFromSplit;
            IsSplitAces = isSplitAces;
            _value = new HandValue(0, false, 0);
        }

        public Hand(IEnumerable<Card> cards, bool isFromSplit = false, bool isSplitAces = false)
            : this(isFromSplit, isSplitAces)
        {
            if (cards == null) throw new ArgumentNullException(nameof(cards));
            foreach (Card card in cards) Add(card);
        }

        public IReadOnlyList<Card> Cards => _cards;

        public int Count => _cards.Count;

        public HandValue Value => _value;

        /// <summary>
        /// True si la mano nació de un split. Importa porque un 21 en mano
        /// splitteada NO es blackjack: paga 1:1, no 3:2.
        /// </summary>
        public bool IsFromSplit { get; }

        /// <summary>
        /// True si viene de partir ases. Estas manos reciben una única carta
        /// y no se pueden volver a pedir ni partir.
        /// </summary>
        public bool IsSplitAces { get; }

        /// <summary>
        /// Blackjack natural: exactamente 2 cartas sumando 21 y sin venir de
        /// un split.
        /// </summary>
        public bool IsBlackjack => _cards.Count == 2 && _value.Total == 21 && !IsFromSplit;

        public bool IsBust => _value.IsBust;

        /// <summary>
        /// True si es un par partible. Compara por valor de puntos, no por
        /// figura: casi todos los casinos dejan partir K-10 o Q-J porque
        /// ambas valen 10. Ver <see cref="Rules.TableRules.SplitByExactRank"/>
        /// para exigir figura idéntica.
        /// </summary>
        public bool IsPair
        {
            get
            {
                if (_cards.Count != 2) return false;
                return _cards[0].HardValue == _cards[1].HardValue;
            }
        }

        /// <summary>True si las 2 cartas son exactamente de la misma figura.</summary>
        public bool IsExactRankPair
            => _cards.Count == 2 && _cards[0].Rank == _cards[1].Rank;

        public void Add(Card card)
        {
            _cards.Add(card);
            _value = Evaluate(_cards);
        }

        /// <summary>
        /// Cuenta puntos promoviendo un único As a 11 si cabe.
        ///
        /// Nunca puede haber dos ases valiendo 11 a la vez (11+11 = 22), así
        /// que basta con sumar todo en duro y mirar si sobran 10 puntos.
        /// </summary>
        public static HandValue Evaluate(IReadOnlyList<Card> cards)
        {
            if (cards == null) throw new ArgumentNullException(nameof(cards));

            int hard = 0;
            bool hasAce = false;

            for (int i = 0; i < cards.Count; i++)
            {
                hard += cards[i].HardValue;
                if (cards[i].IsAce) hasAce = true;
            }

            bool isSoft = hasAce && hard + 10 <= 21;
            int total = isSoft ? hard + 10 : hard;

            return new HandValue(total, isSoft, hard);
        }

        public override string ToString()
        {
            var sb = new StringBuilder();
            for (int i = 0; i < _cards.Count; i++)
            {
                if (i > 0) sb.Append(' ');
                sb.Append(_cards[i]);
            }
            sb.Append(" = ").Append(_value);
            if (IsBlackjack) sb.Append(" ¡BLACKJACK!");
            return sb.ToString();
        }
    }
}
