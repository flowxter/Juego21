using System;
using Blackjack.Core.Cards;

namespace Blackjack.Core.SideBets
{
    public enum TwentyOnePlus3Category : byte
    {
        None = 0,

        /// <summary>Tres cartas del mismo palo. Paga 5:1.</summary>
        Flush = 1,

        /// <summary>Tres figuras consecutivas. Paga 10:1.</summary>
        Straight = 2,

        /// <summary>Tres cartas de la misma figura, palos distintos. Paga 30:1.</summary>
        ThreeOfAKind = 3,

        /// <summary>Escalera del mismo palo. Paga 40:1.</summary>
        StraightFlush = 4,

        /// <summary>Trío de la misma figura Y el mismo palo. Paga 100:1.</summary>
        SuitedTrips = 5
    }

    /// <summary>
    /// 21+3: combina las dos cartas del jugador con la carta descubierta del
    /// croupier y las puntúa como una mano de póker de tres cartas.
    ///
    /// Solo es posible con carta descubierta, así que en mesas europeas sin
    /// carta tapada funciona igual, pero exige que la upcard ya esté sobre la
    /// mesa antes de resolver.
    /// </summary>
    public sealed class TwentyOnePlus3
    {
        public TwentyOnePlus3(
            decimal flushPayout = 5m,
            decimal straightPayout = 10m,
            decimal threeOfAKindPayout = 30m,
            decimal straightFlushPayout = 40m,
            decimal suitedTripsPayout = 100m)
        {
            FlushPayout = flushPayout;
            StraightPayout = straightPayout;
            ThreeOfAKindPayout = threeOfAKindPayout;
            StraightFlushPayout = straightFlushPayout;
            SuitedTripsPayout = suitedTripsPayout;
        }

        public decimal FlushPayout { get; }

        public decimal StraightPayout { get; }

        public decimal ThreeOfAKindPayout { get; }

        public decimal StraightFlushPayout { get; }

        public decimal SuitedTripsPayout { get; }

        /// <summary>Tabla escalonada: 5 / 10 / 30 / 40 / 100.</summary>
        public static TwentyOnePlus3 Standard => new TwentyOnePlus3();

        public static TwentyOnePlus3Category Categorize(Card playerFirst, Card playerSecond, Card dealerUpcard)
        {
            bool isFlush = playerFirst.Suit == playerSecond.Suit
                        && playerSecond.Suit == dealerUpcard.Suit;

            bool isTrips = playerFirst.Rank == playerSecond.Rank
                        && playerSecond.Rank == dealerUpcard.Rank;

            // Un trío del mismo palo exige tres barajas distintas en el zapato,
            // por eso paga tanto: con 6 barajas es raro pero perfectamente posible.
            if (isTrips && isFlush) return TwentyOnePlus3Category.SuitedTrips;
            if (isTrips) return TwentyOnePlus3Category.ThreeOfAKind;

            bool isStraight = IsStraight(playerFirst, playerSecond, dealerUpcard);

            if (isStraight && isFlush) return TwentyOnePlus3Category.StraightFlush;
            if (isStraight) return TwentyOnePlus3Category.Straight;
            if (isFlush) return TwentyOnePlus3Category.Flush;

            return TwentyOnePlus3Category.None;
        }

        /// <summary>
        /// Escalera de tres cartas. El As cuenta alto o bajo, así que valen
        /// tanto A-2-3 como Q-K-A. No es circular: K-A-2 no es escalera.
        /// </summary>
        private static bool IsStraight(Card a, Card b, Card c)
        {
            // As alto (A = 14): cubre Q-K-A.
            if (IsConsecutive((int)a.Rank, (int)b.Rank, (int)c.Rank)) return true;

            // As bajo (A = 1): cubre A-2-3.
            return IsConsecutive(a.Rank.LowAceOrdinal(), b.Rank.LowAceOrdinal(), c.Rank.LowAceOrdinal());
        }

        private static bool IsConsecutive(int x, int y, int z)
        {
            int lo = Math.Min(x, Math.Min(y, z));
            int hi = Math.Max(x, Math.Max(y, z));
            int mid = x + y + z - lo - hi;

            return mid == lo + 1 && hi == mid + 1;
        }

        public SideBetResolution Resolve(Card playerFirst, Card playerSecond, Card dealerUpcard, decimal bet)
        {
            if (bet < 0m) throw new ArgumentOutOfRangeException(nameof(bet));

            TwentyOnePlus3Category category = Categorize(playerFirst, playerSecond, dealerUpcard);

            switch (category)
            {
                case TwentyOnePlus3Category.SuitedTrips:
                    return new SideBetResolution("Trío del mismo palo", bet, SuitedTripsPayout);
                case TwentyOnePlus3Category.StraightFlush:
                    return new SideBetResolution("Escalera de color", bet, StraightFlushPayout);
                case TwentyOnePlus3Category.ThreeOfAKind:
                    return new SideBetResolution("Trío", bet, ThreeOfAKindPayout);
                case TwentyOnePlus3Category.Straight:
                    return new SideBetResolution("Escalera", bet, StraightPayout);
                case TwentyOnePlus3Category.Flush:
                    return new SideBetResolution("Color", bet, FlushPayout);
                default:
                    return new SideBetResolution(string.Empty, bet, 0m);
            }
        }
    }
}
