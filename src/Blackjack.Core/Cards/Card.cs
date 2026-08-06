using System;

namespace Blackjack.Core.Cards
{
    /// <summary>
    /// Una carta concreta. Es un struct inmutable de 2 bytes: se copian
    /// millones en las simulaciones de balanceo y no queremos presión de GC.
    /// </summary>
    public readonly struct Card : IEquatable<Card>
    {
        public Rank Rank { get; }

        public Suit Suit { get; }

        public Card(Rank rank, Suit suit)
        {
            Rank = rank;
            Suit = suit;
        }

        public CardColor Color => Suit.Color();

        /// <summary>Puntos con el As valiendo 1. Ver <see cref="RankExtensions.HardValue"/>.</summary>
        public int HardValue => Rank.HardValue();

        public bool IsAce => Rank == Rank.Ace;

        public bool IsTenValued => Rank.IsTenValued();

        /// <summary>
        /// Identificador compacto 0..51 para enviar por red. El cliente lo
        /// traduce a sprite; nunca recibe la baraja, solo cartas ya repartidas.
        /// </summary>
        public byte Id => (byte)(((int)Rank - 2) * 4 + (int)Suit);

        public static Card FromId(byte id)
        {
            if (id > 51) throw new ArgumentOutOfRangeException(nameof(id), id, "El id de carta debe estar entre 0 y 51.");
            return new Card((Rank)((id / 4) + 2), (Suit)(id % 4));
        }

        public bool Equals(Card other) => Rank == other.Rank && Suit == other.Suit;

        public override bool Equals(object? obj) => obj is Card other && Equals(other);

        public override int GetHashCode() => Id;

        public static bool operator ==(Card left, Card right) => left.Equals(right);

        public static bool operator !=(Card left, Card right) => !left.Equals(right);

        public override string ToString() => Rank.ShortName() + Suit.Symbol();
    }
}
