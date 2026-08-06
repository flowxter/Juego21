namespace Blackjack.Core.Cards
{
    /// <summary>
    /// Valor nominal de la carta. Los números coinciden con el orden natural
    /// para escaleras (As alto = 14); el As bajo se trata como caso especial
    /// donde hace falta, y su valor en blackjack lo resuelve la mano completa
    /// (ver Blackjack.Core.Hands.Hand).
    /// </summary>
    public enum Rank : byte
    {
        Two = 2,
        Three = 3,
        Four = 4,
        Five = 5,
        Six = 6,
        Seven = 7,
        Eight = 8,
        Nine = 9,
        Ten = 10,
        Jack = 11,
        Queen = 12,
        King = 13,
        Ace = 14
    }

    public static class RankExtensions
    {
        /// <summary>
        /// Puntos en blackjack contando el As como 1. La promoción a 11 la
        /// decide la mano completa, no la carta suelta: una carta no sabe si
        /// puede permitirse ser blanda.
        /// </summary>
        public static int HardValue(this Rank rank)
        {
            if (rank == Rank.Ace) return 1;
            return (int)rank >= (int)Rank.Ten ? 10 : (int)rank;
        }

        /// <summary>
        /// True si la carta vale 10 (diez, J, Q, K). Se usa en el peek del
        /// dealer y en la detección de blackjack.
        /// </summary>
        public static bool IsTenValued(this Rank rank)
        {
            return rank == Rank.Ten || rank == Rank.Jack
                || rank == Rank.Queen || rank == Rank.King;
        }

        /// <summary>
        /// Índice para escaleras con As bajo (A=1, 2=2 ... K=13). 21+3 acepta
        /// tanto A-2-3 como Q-K-A, así que necesitamos ambas lecturas.
        /// </summary>
        public static int LowAceOrdinal(this Rank rank)
        {
            return rank == Rank.Ace ? 1 : (int)rank;
        }

        public static string ShortName(this Rank rank)
        {
            switch (rank)
            {
                case Rank.Ace: return "A";
                case Rank.King: return "K";
                case Rank.Queen: return "Q";
                case Rank.Jack: return "J";
                case Rank.Ten: return "10";
                default: return ((int)rank).ToString();
            }
        }
    }
}
