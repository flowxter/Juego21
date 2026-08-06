namespace Blackjack.Core.Cards
{
    /// <summary>
    /// Palo de una carta. El orden sigue el convenio de bridge (tréboles bajo,
    /// picas alto), que es el que usan los desempates de las side bets.
    /// </summary>
    public enum Suit : byte
    {
        Clubs = 0,
        Diamonds = 1,
        Hearts = 2,
        Spades = 3
    }

    public static class SuitExtensions
    {
        /// <summary>
        /// Color del palo. Lo necesita Perfect Pairs para distinguir el par
        /// mixto (6:1) del par del mismo color (12:1).
        /// </summary>
        public static CardColor Color(this Suit suit)
        {
            return suit == Suit.Diamonds || suit == Suit.Hearts
                ? CardColor.Red
                : CardColor.Black;
        }

        /// <summary>Símbolo Unicode del palo, para logs y depuración.</summary>
        public static string Symbol(this Suit suit)
        {
            switch (suit)
            {
                case Suit.Clubs: return "♣";
                case Suit.Diamonds: return "♦";
                case Suit.Hearts: return "♥";
                case Suit.Spades: return "♠";
                default: return "?";
            }
        }
    }

    public enum CardColor : byte
    {
        Red = 0,
        Black = 1
    }
}
